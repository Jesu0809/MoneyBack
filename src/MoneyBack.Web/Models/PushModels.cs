namespace MoneyBack.Web.Models;

public record VapidPublicKeyResponse(string PublicKey);

public record SuscribirsePushRequest(string Endpoint, string P256dh, string Auth);

/// <param name="Tipo">Nombre del enum del API: General, Tope, CobroFijo, Resumen, GastoAutomatico, Invitacion.</param>
public record NotificacionResponse(
    int Id, string Tipo, string Titulo, string Cuerpo, string? Url, DateTime CreadaEn, bool Leida);

public record BandejaNotificacionesResponse(List<NotificacionResponse> Notificaciones, int SinLeer);
