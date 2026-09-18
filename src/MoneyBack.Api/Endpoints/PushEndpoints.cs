using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MoneyBack.Api.Config;
using MoneyBack.Api.Data;
using MoneyBack.Api.Dtos;
using MoneyBack.Api.Models.Notificaciones;
using MoneyBack.Api.Services;

namespace MoneyBack.Api.Endpoints;

public static class PushEndpoints
{
    public static void MapPushEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/push").WithTags("Push").RequireAuthorization();

        group.MapGet("/vapid-public-key", (IOptions<PushOptions> opciones) =>
            Results.Ok(new VapidPublicKeyResponse(opciones.Value.VapidPublicKey)));

        group.MapPost("/suscribirse", async (SuscribirsePushRequest request, ClaimsPrincipal principal, ApplicationDbContext db, PushNotificationSender sender) =>
        {
            var usuarioId = principal.GetUsuarioId();
            var existente = await db.SuscripcionesPush.FirstOrDefaultAsync(s => s.Endpoint == request.Endpoint);

            if (existente is not null)
            {
                existente.UsuarioId = usuarioId;
                existente.P256dh = request.P256dh;
                existente.Auth = request.Auth;
            }
            else
            {
                db.SuscripcionesPush.Add(new SuscripcionPush
                {
                    UsuarioId = usuarioId,
                    Endpoint = request.Endpoint,
                    P256dh = request.P256dh,
                    Auth = request.Auth
                });
            }

            await db.SaveChangesAsync();

            // Push de prueba inmediato: para que el usuario pueda confirmar
            // en el momento que sí le llegan, en vez de esperar a que se
            // acerque un cobro programado de verdad.
            await sender.EnviarATodosLosDispositivosAsync(
                usuarioId, "MoneyBack", "¡Notificaciones activadas! Así se van a ver los avisos de tus cobros programados.");

            return Results.NoContent();
        });

        group.MapDelete("/suscribirse", async ([FromBody] SuscribirsePushRequest request, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var usuarioId = principal.GetUsuarioId();
            var existente = await db.SuscripcionesPush
                .FirstOrDefaultAsync(s => s.Endpoint == request.Endpoint && s.UsuarioId == usuarioId);

            if (existente is not null)
            {
                db.SuscripcionesPush.Remove(existente);
                await db.SaveChangesAsync();
            }

            return Results.NoContent();
        });
    }
}
