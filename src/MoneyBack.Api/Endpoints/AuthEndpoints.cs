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

            var codigoValido = await db.CodigosInvitacion
                .Where(c => c.Activo)
                .AnyAsync(c => c.CodigoHash == TokenService.HashearToken(request.CodigoInvitacion.Trim()));

            if (!codigoValido)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["codigoInvitacion"] = ["El código de invitación no es válido."]
                });
            }

            var usuario = new Usuario { UserName = request.Email, Email = request.Email, Nombre = request.Nombre };
            var resultado = await userManager.CreateAsync(usuario, request.Password);

            if (!resultado.Succeeded)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["password"] = resultado.Errors.Select(e => e.Description).ToArray()
                });
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
                // Reuso de un refresh token ya rotado: posible robo. Se revoca toda la sesión del usuario.
                var tokensDelUsuario = await db.RefreshTokens
                    .Where(t => t.UsuarioId == tokenGuardado.UsuarioId && t.RevocadoEn == null)
                    .ToListAsync();
                foreach (var t in tokensDelUsuario) t.RevocadoEn = DateTime.UtcNow;
                await db.SaveChangesAsync();
                return Results.Unauthorized();
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
            return Results.Ok(new PerfilResponse(usuario.Id, usuario.Nombre, usuario.Email!, roles.ToList()));
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
            return Results.Ok(new PerfilResponse(usuario.Id, usuario.Nombre, usuario.Email!, roles.ToList()));
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
