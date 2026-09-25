using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using MoneyBack.Api.Data;
using MoneyBack.Api.Dtos;
using MoneyBack.Api.Models.Auth;
using MoneyBack.Api.Services;

namespace MoneyBack.Api.Endpoints;

/// <summary>
/// Deja que cualquiera invite a alguien más a la app, sin pasar por un
/// administrador. Quien entra por acá arma su propio hogar con su pareja: la
/// invitación abre la puerta de MoneyBack, no la del hogar de quien invita
/// (eso es InvitacionesHogarEndpoints, y es otra decisión distinta).
/// </summary>
public static class InvitacionesAppEndpoints
{
    /// <summary>
    /// Cuántas invitaciones sin usar puede tener alguien a la vez. No es
    /// desconfianza: es que un montón de enlaces vivos repartidos por ahí son
    /// justo lo que convierte una app por invitación en una puerta abierta.
    /// Se liberan solas al usarse, vencerse o anularse.
    /// </summary>
    private const int MaximoPendientes = 5;

    public static void MapInvitacionesAppEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/invitaciones-app").WithTags("Auth").RequireAuthorization();

        group.MapGet("/", async (ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var usuarioId = principal.GetUsuarioId();
            var ahora = DateTime.UtcNow;

            var invitaciones = await db.InvitacionesApp
                .Where(i => i.CreadoPorUsuarioId == usuarioId)
                .Include(i => i.UsadaPor)
                .OrderByDescending(i => i.CreadoEn)
                .ToListAsync();

            return Results.Ok(invitaciones.Select(i => new InvitacionAppResponse(
                i.Id, Estado(i, ahora), i.CreadoEn, i.ExpiraEn, i.UsadaPor != null ? i.UsadaPor.Nombre : null)));
        });

        group.MapPost("/", async (ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var usuarioId = principal.GetUsuarioId();
            var ahora = DateTime.UtcNow;

            var pendientes = await db.InvitacionesApp
                .CountAsync(i => i.CreadoPorUsuarioId == usuarioId
                    && !i.Anulada && i.UsadaEn == null && i.ExpiraEn > ahora);

            if (pendientes >= MaximoPendientes)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["invitaciones"] = [$"Ya tienes {MaximoPendientes} invitaciones sin usar. Anula alguna o espera a que venzan."]
                });
            }

            // Mismo formato que los tokens de atajo: aleatorio de 32 bytes y
            // seguro para URL, porque este viaja dentro de un enlace.
            var token = "inv_" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .Replace("+", "").Replace("/", "").Replace("=", "")[..32];

            var invitacion = new InvitacionApp
            {
                CreadoPorUsuarioId = usuarioId,
                TokenHash = TokenService.HashearToken(token)
            };

            db.InvitacionesApp.Add(invitacion);
            await db.SaveChangesAsync();

            // El token en claro se devuelve una sola vez, acá. Después solo
            // queda su hash, así que ni el servidor puede reconstruir el
            // enlace — por eso la app lo copia al portapapeles de una.
            return Results.Ok(new InvitacionAppCreadaResponse(invitacion.Id, token, invitacion.ExpiraEn));
        });

        group.MapDelete("/{id:int}", async (int id, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var usuarioId = principal.GetUsuarioId();

            var invitacion = await db.InvitacionesApp
                .FirstOrDefaultAsync(i => i.Id == id && i.CreadoPorUsuarioId == usuarioId);

            if (invitacion is null) return Results.NotFound();

            // Anular una ya usada no tendría efecto —la persona ya está
            // adentro— pero sí borraría el rastro de quién invitó a quién.
            if (invitacion.UsadaEn is not null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["invitacion"] = ["Esa invitación ya se usó. Anularla no sacaría a nadie de la app."]
                });
            }

            invitacion.Anulada = true;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    private static string Estado(InvitacionApp invitacion, DateTime ahora) =>
        invitacion.UsadaEn is not null ? "Usada"
        : invitacion.Anulada ? "Anulada"
        : invitacion.ExpiraEn <= ahora ? "Vencida"
        : "Pendiente";
}
