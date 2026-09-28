using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MoneyBack.Api.Data;
using MoneyBack.Api.Dtos;
using MoneyBack.Api.Models;
using MoneyBack.Api.Models.Auth;
using MoneyBack.Api.Services;

namespace MoneyBack.Api.Endpoints;

/// <summary>
/// Todo lo administrativo (gestionar código de invitación, revocar sesiones,
/// listar cuentas). Deliberadamente NO expone MetaAhorro/MovimientoMeta ni
/// nada de plata de un hogar: eso solo lo ven los dos usuarios del hogar.
/// </summary>
public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin")
            .WithTags("Admin")
            .RequireAuthorization(policy => policy.RequireRole(Roles.SuperAdmin));

        group.MapGet("/usuarios", async (UserManager<Usuario> userManager) =>
        {
            var usuarios = await userManager.Users.OrderBy(u => u.FechaCreacion).ToListAsync();
            var respuesta = new List<UsuarioAdminResponse>();
            foreach (var usuario in usuarios)
            {
                var roles = await userManager.GetRolesAsync(usuario);
                respuesta.Add(new UsuarioAdminResponse(usuario.Id, usuario.Nombre, usuario.Email!, usuario.FechaCreacion, roles.ToList()));
            }
            return Results.Ok(respuesta);
        });

        group.MapPost("/usuarios/{id:int}/revocar-sesiones", async (int id, ApplicationDbContext db) =>
        {
            var tokensActivos = await db.RefreshTokens
                .Where(t => t.UsuarioId == id && t.RevocadoEn == null)
                .ToListAsync();

            foreach (var token in tokensActivos) token.RevocadoEn = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Results.Ok(new { sesionesRevocadas = tokensActivos.Count });
        });

        // Le pone una contraseña temporal a una cuenta y cierra todas sus
        // sesiones. El administrador nunca ve la contraseña anterior —no
        // existe en ninguna parte, solo su hash— y la temporal se muestra
        // una sola vez, acá mismo, para pasársela a la persona.
        //
        // Se cierran las sesiones a propósito: si alguien entró con la
        // contraseña vieja, cambiarla sin sacarlo no sirve de nada. Y se
        // borran los códigos de recuperación pendientes porque después de
        // un reajuste administrativo ya no se sabe quién los tiene.
        group.MapPost("/usuarios/{id:int}/clave-temporal", async (
            int id,
            ClaimsPrincipal principal,
            UserManager<Usuario> userManager,
            ApplicationDbContext db,
            ILoggerFactory loggerFactory) =>
        {
            var usuario = await userManager.FindByIdAsync(id.ToString());
            if (usuario is null) return Results.NotFound();

            var temporal = GenerarClaveTemporal();

            var token = await userManager.GeneratePasswordResetTokenAsync(usuario);
            var resultado = await userManager.ResetPasswordAsync(usuario, token, temporal);

            if (!resultado.Succeeded)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["clave"] = resultado.Errors.Select(e => e.Description).ToArray()
                });
            }

            var sesiones = await db.RefreshTokens
                .Where(t => t.UsuarioId == usuario.Id && t.RevocadoEn == null)
                .ToListAsync();
            foreach (var sesion in sesiones) sesion.RevocadoEn = DateTime.UtcNow;

            var codigos = await db.CodigosRecuperacion.Where(c => c.UsuarioId == usuario.Id).ToListAsync();
            db.CodigosRecuperacion.RemoveRange(codigos);

            await db.SaveChangesAsync();

            // Queda en el registro del servidor: un cambio de contraseña
            // hecho por otra persona no puede ser invisible.
            loggerFactory.CreateLogger("Admin").LogWarning(
                "El usuario {Admin} le puso clave temporal a la cuenta {Objetivo} y cerró {Sesiones} sesión(es).",
                principal.GetUsuarioId(), usuario.Id, sesiones.Count);

            return Results.Ok(new ClaveTemporalResponse(temporal, sesiones.Count));
        });

        group.MapGet("/codigo-invitacion", async (ApplicationDbContext db) =>
        {
            var actual = await db.CodigosInvitacion
                .Where(c => c.Activo)
                .OrderByDescending(c => c.CreadoEn)
                .Select(c => new { c.Id, c.CreadoEn })
                .FirstOrDefaultAsync();

            return actual is null ? Results.NotFound() : Results.Ok(actual);
        });

        group.MapPost("/codigo-invitacion/rotar", async (RotarCodigoInvitacionRequest request, ApplicationDbContext db, ClaimsPrincipal principal) =>
        {
            var nuevoCodigo = request.NuevoCodigo.Trim();
            if (nuevoCodigo.Length < 8)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["nuevoCodigo"] = ["El código de invitación debe tener al menos 8 caracteres."]
                });
            }

            var activos = await db.CodigosInvitacion.Where(c => c.Activo).ToListAsync();
            foreach (var c in activos) c.Activo = false;

            db.CodigosInvitacion.Add(new CodigoInvitacion
            {
                CodigoHash = TokenService.HashearToken(nuevoCodigo),
                CreadoPorUsuarioId = principal.GetUsuarioId()
            });

            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    /// <summary>
    /// Se escribe a mano en el teclado de un celular, así que va sin las
    /// letras y números que se confunden al leer (0/O, 1/I/L) y con guion
    /// en la mitad. Lleva mayúscula, minúscula, dígito y símbolo para pasar
    /// las reglas de Identity sin depender de la suerte del azar.
    /// </summary>
    private const string Mayusculas = "ABCDEFGHJKMNPQRSTUVWXYZ";
    private const string Minusculas = "abcdefghijkmnpqrstuvwxyz";
    private const string Digitos = "23456789";

    private static string GenerarClaveTemporal()
    {
        var caracteres = new List<char>
        {
            Mayusculas[RandomNumberGenerator.GetInt32(Mayusculas.Length)],
            Minusculas[RandomNumberGenerator.GetInt32(Minusculas.Length)],
            Digitos[RandomNumberGenerator.GetInt32(Digitos.Length)],
            '#'
        };

        var alfabeto = Mayusculas + Minusculas + Digitos;
        while (caracteres.Count < 10)
        {
            caracteres.Add(alfabeto[RandomNumberGenerator.GetInt32(alfabeto.Length)]);
        }

        // Se baraja para que el símbolo y los obligatorios no queden siempre
        // en la misma posición.
        for (var i = caracteres.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (caracteres[i], caracteres[j]) = (caracteres[j], caracteres[i]);
        }

        return new string(caracteres.Take(5).ToArray()) + "-" + new string(caracteres.Skip(5).ToArray());
    }
}
