using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MoneyBack.Api.Data;
using MoneyBack.Api.Dtos;
using MoneyBack.Api.Services;

namespace MoneyBack.Api.Endpoints;

/// <summary>
/// El historial de avisos de una persona.
///
/// Hasta ahora la campanita solo mostraba cobros por confirmar: las alertas
/// de topes y el resumen semanal salían por push y no dejaban rastro, así
/// que si no se veía la notificación en el momento, el aviso no había
/// existido. Acá queda todo lo que se le mandó.
/// </summary>
public static class NotificacionesEndpoints
{
    /// <summary>
    /// Cuántas se traen de una. Con más de esto la lista deja de leerse y
    /// empieza a pesar; lo viejo se consulta en los reportes, no acá.
    /// </summary>
    private const int Tope = 50;

    public static void MapNotificacionesEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/notificaciones").WithTags("Notificaciones").RequireAuthorization();

        group.MapGet("/", async (ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var usuarioId = principal.GetUsuarioId();

            var notificaciones = await db.Notificaciones
                .Where(n => n.UsuarioId == usuarioId)
                .OrderByDescending(n => n.CreadaEn)
                .Take(Tope)
                .Select(n => new NotificacionResponse(
                    n.Id, n.Tipo.ToString(), n.Titulo, n.Cuerpo, n.Url, n.CreadaEn, n.LeidaEn != null))
                .ToListAsync();

            var sinLeer = await db.Notificaciones
                .CountAsync(n => n.UsuarioId == usuarioId && n.LeidaEn == null);

            return Results.Ok(new BandejaNotificacionesResponse(notificaciones, sinLeer));
        });

        group.MapPost("/{id:int}/leer", async (int id, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var usuarioId = principal.GetUsuarioId();
            var notificacion = await db.Notificaciones.FirstOrDefaultAsync(n => n.Id == id);

            if (notificacion is null) return Results.NotFound();
            if (notificacion.UsuarioId != usuarioId) return Results.Forbid();

            // Se conserva la fecha de la primera lectura: volver a abrir el
            // panel no debería mover ese dato.
            notificacion.LeidaEn ??= DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Results.NoContent();
        });

        group.MapPost("/leer-todas", async (ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var usuarioId = principal.GetUsuarioId();

            // Sin ExecuteUpdateAsync a propósito: las pruebas corren sobre EF
            // InMemory, que no lo soporta. Son como máximo unas decenas de
            // filas, así que traerlas no cuesta nada.
            var sinLeer = await db.Notificaciones
                .Where(n => n.UsuarioId == usuarioId && n.LeidaEn == null)
                .ToListAsync();

            var ahora = DateTime.UtcNow;
            foreach (var n in sinLeer) n.LeidaEn = ahora;
            await db.SaveChangesAsync();

            return Results.Ok(new { marcadas = sinLeer.Count });
        });
    }
}
