using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MoneyBack.Api.Config;
using MoneyBack.Api.Data;
using WebPush;

namespace MoneyBack.Api.Services;

/// <summary>
/// Envía notificaciones push a todos los dispositivos registrados de un
/// usuario. Si el servicio push responde 404/410 (endpoint caducado — algo
/// normal, pasa cuando el usuario desinstala la PWA o cambia de navegador),
/// borra esa suscripción; si no se limpian, se acumulan para siempre y cada
/// ciclo de envío pierde tiempo contra endpoints muertos.
/// </summary>
public class PushNotificationSender(ApplicationDbContext db, IOptions<PushOptions> opciones, ILogger<PushNotificationSender> logger)
{
    public async Task EnviarATodosLosDispositivosAsync(int usuarioId, string titulo, string cuerpo, string? url = null)
    {
        var opts = opciones.Value;
        if (string.IsNullOrEmpty(opts.VapidPublicKey) || string.IsNullOrEmpty(opts.VapidPrivateKey))
        {
            logger.LogWarning("Push no configurado (faltan llaves VAPID) — se omite el envío.");
            return;
        }

        var suscripciones = await db.SuscripcionesPush.Where(s => s.UsuarioId == usuarioId).ToListAsync();
        if (suscripciones.Count == 0) return;

        var vapidDetails = new VapidDetails(opts.VapidSubject, opts.VapidPublicKey, opts.VapidPrivateKey);
        var cliente = new WebPushClient();
        var payload = JsonSerializer.Serialize(new { titulo, cuerpo, url });

        var huboVencidas = false;

        foreach (var suscripcion in suscripciones)
        {
            try
            {
                var destino = new PushSubscription(suscripcion.Endpoint, suscripcion.P256dh, suscripcion.Auth);
                await cliente.SendNotificationAsync(destino, payload, vapidDetails);
            }
            catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
            {
                db.SuscripcionesPush.Remove(suscripcion);
                huboVencidas = true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error enviando push a la suscripción {Id}", suscripcion.Id);
            }
        }

        if (huboVencidas) await db.SaveChangesAsync();
    }
}
