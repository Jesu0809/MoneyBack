using MoneyBack.Api.Models;

namespace MoneyBack.Api.Models.Notificaciones;

/// <summary>
/// Una suscripción de Web Push de un dispositivo/navegador concreto. Un
/// Usuario puede tener varias (celular, computador, etc). Endpoint+P256dh+
/// Auth son los tres datos que entrega pushManager.subscribe() en el
/// navegador — se reenvían tal cual a la librería WebPush al enviar.
///
/// Se llama "SuscripcionPush" y no "PushSubscription" a propósito: ese
/// nombre choca con el tipo WebPush.PushSubscription de la librería del
/// mismo nombre, que se usa en el mismo archivo que envía los push
/// (PushNotificationSender).
/// </summary>
public class SuscripcionPush
{
    public int Id { get; set; }

    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public string Endpoint { get; set; } = string.Empty;

    public string P256dh { get; set; } = string.Empty;

    public string Auth { get; set; } = string.Empty;

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
}
