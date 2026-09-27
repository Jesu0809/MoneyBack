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
/// Recuperar la cuenta sin depender del correo ni de un administrador.
///
/// La persona guarda una lista de claves de un solo uso; con cualquiera de
/// ellas y su correo puede ponerse una contraseña nueva. Es el mismo
/// mecanismo que usan los bancos y GitHub como respaldo del segundo factor,
/// y evita montar envío de correos —servicio, dominio verificado, pelear con
/// el spam— para algo que pasa una vez cada tanto.
/// </summary>
public static class RecuperacionEndpoints
{
    public static void MapRecuperacionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        // Generar códigos exige sesión activa: es algo que se hace ANTES de
        // perder el acceso, no después.
        group.MapPost("/codigos-recuperacion", async (
            ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var usuarioId = principal.GetUsuarioId();

            // Generar una lista nueva invalida la anterior. Si no, una foto
            // vieja de códigos seguiría sirviendo después de que la persona
            // los rotó justamente porque creía que se habían filtrado.
            var anteriores = await db.CodigosRecuperacion
                .Where(c => c.UsuarioId == usuarioId)
                .ToListAsync();
            db.CodigosRecuperacion.RemoveRange(anteriores);

            var codigos = new List<string>();
            for (var i = 0; i < CodigoRecuperacion.CuantosGenerar; i++)
            {
                var codigo = GenerarCodigo();
                codigos.Add(codigo);

                db.CodigosRecuperacion.Add(new CodigoRecuperacion
                {
                    UsuarioId = usuarioId,
                    CodigoHash = TokenService.HashearToken(NormalizarCodigo(codigo))
                });
            }

            await db.SaveChangesAsync();

            // En claro una sola vez. Después solo queda el hash, así que ni
            // el servidor puede volver a mostrarlos.
            return Results.Ok(new CodigosRecuperacionResponse(codigos));
        }).RequireAuthorization();

        group.MapGet("/codigos-recuperacion/cuantos-quedan", async (
            ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var usuarioId = principal.GetUsuarioId();
            var quedan = await db.CodigosRecuperacion
                .CountAsync(c => c.UsuarioId == usuarioId && c.UsadoEn == null);

            return Results.Ok(new CodigosRestantesResponse(quedan));
        }).RequireAuthorization();

        group.MapPost("/recuperar", async (
            RecuperarCuentaRequest request,
            UserManager<Usuario> userManager,
            ApplicationDbContext db) =>
        {
            var errores = new Dictionary<string, string[]>();
            if (string.IsNullOrWhiteSpace(request.Email)) errores["email"] = ["El correo es obligatorio."];
            if (string.IsNullOrWhiteSpace(request.Codigo)) errores["codigo"] = ["La clave de recuperación es obligatoria."];
            if (string.IsNullOrWhiteSpace(request.NuevaPassword)) errores["nuevaPassword"] = ["La contraseña nueva es obligatoria."];
            if (errores.Count > 0) return Results.ValidationProblem(errores);

            var usuario = await userManager.FindByEmailAsync(request.Email.Trim());

            // El mismo mensaje exista o no la cuenta, y con el mismo trabajo
            // por detrás: si respondiera distinto, esto serviría para
            // averiguar qué correos están registrados.
            var hash = TokenService.HashearToken(NormalizarCodigo(request.Codigo));

            var codigo = usuario is null
                ? null
                : await db.CodigosRecuperacion.FirstOrDefaultAsync(c =>
                    c.UsuarioId == usuario.Id && c.CodigoHash == hash && c.UsadoEn == null);

            if (usuario is null || codigo is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["codigo"] = ["El correo o la clave de recuperación no coinciden."]
                });
            }

            var token = await userManager.GeneratePasswordResetTokenAsync(usuario);
            var resultado = await userManager.ResetPasswordAsync(usuario, token, request.NuevaPassword);

            if (!resultado.Succeeded)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["nuevaPassword"] = resultado.Errors.Select(e => e.Description).ToArray()
                });
            }

            codigo.UsadoEn = DateTime.UtcNow;

            // Se cierran todas las sesiones abiertas. Si alguien entró con la
            // contraseña vieja, este es el momento de sacarlo: recuperar la
            // cuenta no sirve de nada si el otro sigue adentro.
            var sesiones = await db.RefreshTokens
                .Where(t => t.UsuarioId == usuario.Id && t.RevocadoEn == null)
                .ToListAsync();
            foreach (var sesion in sesiones) sesion.RevocadoEn = DateTime.UtcNow;

            await db.SaveChangesAsync();

            var quedan = await db.CodigosRecuperacion
                .CountAsync(c => c.UsuarioId == usuario.Id && c.UsadoEn == null);

            return Results.Ok(new RecuperacionExitosaResponse(quedan));
        }).RequireRateLimiting("auth");
    }

    /// <summary>
    /// Formato tipo "K7M2-PQ4X": corto para escribirlo a mano en un celular,
    /// y sin las letras y números que se confunden al leer (0/O, 1/I/L).
    /// Alguien va a copiar esto de una foto a las once de la noche.
    /// </summary>
    private const string Alfabeto = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    private static string GenerarCodigo()
    {
        var letras = new char[9];
        for (var i = 0; i < 9; i++)
        {
            letras[i] = i == 4 ? '-' : Alfabeto[RandomNumberGenerator.GetInt32(Alfabeto.Length)];
        }
        return new string(letras);
    }

    /// <summary>
    /// Acepta el código como lo escriba: con minúsculas, sin el guion, con
    /// espacios de más. Rechazarlo por eso sería cruel con alguien que ya
    /// está bloqueado fuera de su cuenta.
    /// </summary>
    private static string NormalizarCodigo(string codigo) =>
        codigo.Trim().Replace(" ", "").Replace("-", "").ToUpperInvariant();
}
