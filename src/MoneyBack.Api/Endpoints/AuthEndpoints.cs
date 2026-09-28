using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MoneyBack.Api.Data;
using MoneyBack.Api.Dtos;
using MoneyBack.Api.Models;
using MoneyBack.Api.Models.Auth;
using MoneyBack.Api.Models.DiaADia;
using MoneyBack.Api.Services;

namespace MoneyBack.Api.Endpoints;

public static class AuthEndpoints
{
    /// <summary>
    /// Cuánto se acepta que un cliente vuelva a presentar el token que
    /// acaba de rotar. Es el tiempo que puede pasar entre que el servidor
    /// responde y el teléfono alcanza a guardar el token nuevo: si iOS
    /// suspende la app justo ahí, el intento siguiente llega con el viejo.
    ///
    /// Un minuto cubre de sobra ese caso y sigue dejando el robo a la vista:
    /// un token robado se usa mucho después, no en el mismo minuto.
    /// </summary>
    private static readonly TimeSpan GraciaPorReusoInmediato = TimeSpan.FromMinutes(1);

    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", async (
            RegistrarUsuarioRequest request,
            UserManager<Usuario> userManager,
            RoleManager<IdentityRole<int>> roleManager,
            ApplicationDbContext db,
            TokenService tokenService) =>
        {
            var errores = new Dictionary<string, string[]>();
            if (string.IsNullOrWhiteSpace(request.Nombre)) errores["nombre"] = ["El nombre es obligatorio."];
            if (string.IsNullOrWhiteSpace(request.Email)) errores["email"] = ["El correo es obligatorio."];
            if (string.IsNullOrWhiteSpace(request.Password)) errores["password"] = ["La contraseña es obligatoria."];
            if (string.IsNullOrWhiteSpace(request.CodigoInvitacion)) errores["codigoInvitacion"] = ["El código de invitación es obligatorio."];

            if (errores.Count > 0)
            {
                return Results.ValidationProblem(errores);
            }

            var codigo = request.CodigoInvitacion.Trim();
            var hash = TokenService.HashearToken(codigo);
            var ahora = DateTime.UtcNow;

            var codigoValido = await db.CodigosInvitacion
                .Where(c => c.Activo)
                .AnyAsync(c => c.CodigoHash == hash);

            // Si no es el código general, puede ser el enlace personal que
            // alguien le mandó. Se reserva ANTES de crear la cuenta: si se
            // marcara después, dos personas abriendo el mismo enlace al mismo
            // tiempo pasarían ambas la validación y entrarían las dos.
            InvitacionApp? invitacionPersonal = null;
            if (!codigoValido)
            {
                invitacionPersonal = await db.InvitacionesApp.FirstOrDefaultAsync(i =>
                    i.TokenHash == hash && !i.Anulada && i.UsadaEn == null && i.ExpiraEn > ahora);

                if (invitacionPersonal is null)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["codigoInvitacion"] = ["Ese código o enlace no sirve. Puede que ya se haya usado o que haya vencido — pídele uno nuevo a quien te invitó."]
                    });
                }

                invitacionPersonal.UsadaEn = ahora;

                try
                {
                    await db.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Alguien más abrió el mismo enlace en este instante y lo
                    // reservó primero.
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["codigoInvitacion"] = ["Ese enlace acaba de usarse. Pídele uno nuevo a quien te invitó."]
                    });
                }
            }

            var usuario = new Usuario { UserName = request.Email, Email = request.Email, Nombre = request.Nombre };
            var resultado = await userManager.CreateAsync(usuario, request.Password);

            if (!resultado.Succeeded)
            {
                // La cuenta no se creó, así que la invitación no se gastó:
                // se devuelve para que la persona pueda reintentar con el
                // mismo enlace en vez de tener que pedir otro.
                if (invitacionPersonal is not null)
                {
                    invitacionPersonal.UsadaEn = null;
                    await db.SaveChangesAsync();
                }

                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["password"] = resultado.Errors.Select(e => e.Description).ToArray()
                });
            }

            if (invitacionPersonal is not null)
            {
                invitacionPersonal.UsadaPorUsuarioId = usuario.Id;
            }

            var yaHaySuperAdmin = await roleManager.RoleExistsAsync(Roles.SuperAdmin) &&
                (await userManager.GetUsersInRoleAsync(Roles.SuperAdmin)).Count > 0;
            if (!yaHaySuperAdmin)
            {
                if (!await roleManager.RoleExistsAsync(Roles.SuperAdmin))
                {
                    await roleManager.CreateAsync(new IdentityRole<int>(Roles.SuperAdmin));
                }
                await userManager.AddToRoleAsync(usuario, Roles.SuperAdmin);
            }

            db.Categorias.AddRange(CategoriasPredefinidas.ParaNuevoUsuario(usuario.Id));
            await db.SaveChangesAsync();

            var roles = await userManager.GetRolesAsync(usuario);
            var respuesta = await EmitirTokensAsync(usuario, roles, db, tokenService);
            return Results.Created($"/api/auth/me", respuesta);
        }).RequireRateLimiting("auth");

        group.MapPost("/login", async (
            LoginRequest request,
            UserManager<Usuario> userManager,
            SignInManager<Usuario> signInManager,
            ApplicationDbContext db,
            TokenService tokenService) =>
        {
            var usuario = await userManager.FindByEmailAsync(request.Email);
            if (usuario is null)
            {
                return Results.Unauthorized();
            }

            var resultado = await signInManager.CheckPasswordSignInAsync(usuario, request.Password, lockoutOnFailure: true);
            if (!resultado.Succeeded)
            {
                return Results.Unauthorized();
            }

            var roles = await userManager.GetRolesAsync(usuario);
            var respuesta = await EmitirTokensAsync(usuario, roles, db, tokenService);
            return Results.Ok(respuesta);
        }).RequireRateLimiting("auth");

        group.MapPost("/refresh", async (
            RefrescarTokenRequest request,
            UserManager<Usuario> userManager,
            ApplicationDbContext db,
            TokenService tokenService) =>
        {
            var hash = TokenService.HashearToken(request.RefreshToken);
            var tokenGuardado = await db.RefreshTokens.Include(t => t.Usuario)
                .FirstOrDefaultAsync(t => t.TokenHash == hash);

            if (tokenGuardado is null)
            {
                return Results.Unauthorized();
            }

            if (tokenGuardado.RevocadoEn is not null)
            {
                // Presentar un token ya rotado es la señal clásica de robo,
                // pero también le pasa a un cliente honesto todo el tiempo:
                // el servidor rota el token y responde, y si el teléfono se
                // suspende, pierde señal o cierra la app en ese instante, el
                // token nuevo nunca se guarda. Al volver a abrir presenta el
                // viejo, sin haber hecho nada malo.
                //
                // Tratar eso como robo cerraba la sesión de raíz y obligaba a
                // escribir la contraseña otra vez. Pasa seguido en un celular,
                // donde iOS suspende la app sin avisar.
                //
                // Por eso hay una ventana de gracia: un reuso inmediato se
                // toma como reintento y se le entrega una sesión nueva. Pasado
                // ese rato ya no hay explicación inocente, y ahí sí se revoca
                // todo.
                // La gracia es SOLO para tokens que se revocaron al rotarse
                // —los que tienen a qué token dieron paso—. Un token revocado
                // a mano no tiene reemplazo, y ahí no hay nada inocente que
                // explicar: alguien cerró esa sesión a propósito.
                //
                // Sin esta distinción, cerrar las sesiones de una cuenta (al
                // reajustarle la contraseña, al quitarle la administración,
                // al sacar a alguien) dejaba un minuto entero en el que el
                // token viejo seguía sirviendo para pedir uno nuevo. O sea,
                // cerrar sesiones no cerraba nada durante ese minuto.
                var seRevocoAlRotarse = tokenGuardado.ReemplazadoPorTokenHash is not null;

                var dentroDeLaGracia = seRevocoAlRotarse
                    && DateTime.UtcNow - tokenGuardado.RevocadoEn.Value <= GraciaPorReusoInmediato;

                if (!dentroDeLaGracia)
                {
                    var tokensDelUsuario = await db.RefreshTokens
                        .Where(t => t.UsuarioId == tokenGuardado.UsuarioId && t.RevocadoEn == null)
                        .ToListAsync();
                    foreach (var t in tokensDelUsuario) t.RevocadoEn = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                    return Results.Unauthorized();
                }

                // Se anula el reemplazo que el cliente nunca llegó a recibir,
                // para que quede una sola cadena viva y el siguiente reuso sí
                // se pueda leer como lo que sea.
                var huerfanos = await db.RefreshTokens
                    .Where(t => t.UsuarioId == tokenGuardado.UsuarioId
                        && t.RevocadoEn == null
                        && t.TokenHash == tokenGuardado.ReemplazadoPorTokenHash)
                    .ToListAsync();
                foreach (var t in huerfanos) t.RevocadoEn = DateTime.UtcNow;

                tokenGuardado.RevocadoEn = null;
            }

            if (DateTime.UtcNow >= tokenGuardado.ExpiraEn)
            {
                return Results.Unauthorized();
            }

            var nuevoRefresh = tokenService.GenerarRefreshToken();
            tokenGuardado.RevocadoEn = DateTime.UtcNow;
            tokenGuardado.ReemplazadoPorTokenHash = nuevoRefresh.TokenHash;

            db.RefreshTokens.Add(new RefreshToken
            {
                UsuarioId = tokenGuardado.UsuarioId,
                TokenHash = nuevoRefresh.TokenHash,
                ExpiraEn = nuevoRefresh.ExpiraEn
            });
            await db.SaveChangesAsync();

            var roles = await userManager.GetRolesAsync(tokenGuardado.Usuario);
            var accessToken = tokenService.GenerarAccessToken(tokenGuardado.Usuario, roles);

            return Results.Ok(new AuthResponse(accessToken, nuevoRefresh.TokenEnClaro, DateTime.UtcNow));
        }).RequireRateLimiting("auth");

        group.MapPost("/logout", async (RefrescarTokenRequest request, ApplicationDbContext db) =>
        {
            var hash = TokenService.HashearToken(request.RefreshToken);
            var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);
            if (token is not null && token.RevocadoEn is null)
            {
                token.RevocadoEn = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
            return Results.NoContent();
        });

        group.MapGet("/me", async (ClaimsPrincipal principal, UserManager<Usuario> userManager) =>
        {
            var usuarioId = principal.GetUsuarioId();
            var usuario = await userManager.FindByIdAsync(usuarioId.ToString());
            if (usuario is null) return Results.NotFound();

            var roles = await userManager.GetRolesAsync(usuario);
            return Results.Ok(new PerfilResponse(usuario.Id, usuario.Nombre, usuario.Email!, roles.ToList(), usuario.DiaPago1, usuario.DiaPago2));
        }).RequireAuthorization();

        group.MapPut("/me", async (ActualizarPerfilRequest request, ClaimsPrincipal principal, UserManager<Usuario> userManager) =>
        {
            if (string.IsNullOrWhiteSpace(request.Nombre))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["nombre"] = ["El nombre es obligatorio."]
                });
            }

            var usuario = await userManager.FindByIdAsync(principal.GetUsuarioId().ToString());
            if (usuario is null) return Results.NotFound();

            usuario.Nombre = request.Nombre.Trim();
            await userManager.UpdateAsync(usuario);

            var roles = await userManager.GetRolesAsync(usuario);
            return Results.Ok(new PerfilResponse(usuario.Id, usuario.Nombre, usuario.Email!, roles.ToList(), usuario.DiaPago1, usuario.DiaPago2));
        }).RequireAuthorization();

        group.MapPut("/me/dias-pago", async (ActualizarDiasPagoRequest request, ClaimsPrincipal principal, UserManager<Usuario> userManager) =>
        {
            foreach (var dia in new[] { request.DiaPago1, request.DiaPago2 })
            {
                if (dia is < 1 or > 31)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["diaPago"] = ["El día debe estar entre 1 y 31."]
                    });
                }
            }

            var usuario = await userManager.FindByIdAsync(principal.GetUsuarioId().ToString());
            if (usuario is null) return Results.NotFound();

            usuario.DiaPago1 = request.DiaPago1;
            usuario.DiaPago2 = request.DiaPago2;
            await userManager.UpdateAsync(usuario);

            var roles = await userManager.GetRolesAsync(usuario);
            return Results.Ok(new PerfilResponse(usuario.Id, usuario.Nombre, usuario.Email!, roles.ToList(), usuario.DiaPago1, usuario.DiaPago2));
        }).RequireAuthorization();

        group.MapPost("/cambiar-password", async (CambiarPasswordRequest request, ClaimsPrincipal principal, UserManager<Usuario> userManager) =>
        {
            if (string.IsNullOrWhiteSpace(request.PasswordActual) || string.IsNullOrWhiteSpace(request.PasswordNueva))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["password"] = ["Debes indicar la contraseña actual y la nueva."]
                });
            }

            var usuario = await userManager.FindByIdAsync(principal.GetUsuarioId().ToString());
            if (usuario is null) return Results.NotFound();

            var resultado = await userManager.ChangePasswordAsync(usuario, request.PasswordActual, request.PasswordNueva);
            if (!resultado.Succeeded)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["password"] = resultado.Errors.Select(e => e.Description).ToArray()
                });
            }

            return Results.NoContent();
        }).RequireAuthorization().RequireRateLimiting("auth");
    }

    private static async Task<AuthResponse> EmitirTokensAsync(
        Usuario usuario, IList<string> roles, ApplicationDbContext db, TokenService tokenService)
    {
        var accessToken = tokenService.GenerarAccessToken(usuario, roles);
        var refresh = tokenService.GenerarRefreshToken();

        db.RefreshTokens.Add(new RefreshToken
        {
            UsuarioId = usuario.Id,
            TokenHash = refresh.TokenHash,
            ExpiraEn = refresh.ExpiraEn
        });
        await db.SaveChangesAsync();

        return new AuthResponse(accessToken, refresh.TokenEnClaro, DateTime.UtcNow);
    }
}
