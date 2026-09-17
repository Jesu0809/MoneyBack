using System.Security.Claims;
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
}
