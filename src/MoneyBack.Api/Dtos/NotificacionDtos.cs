namespace MoneyBack.Api.Dtos;

/// <param name="Tipo">El nombre del enum, para que el cliente elija el ícono.</param>
public record NotificacionResponse(
    int Id, string Tipo, string Titulo, string Cuerpo, string? Url, DateTime CreadaEn, bool Leida);

public record BandejaNotificacionesResponse(List<NotificacionResponse> Notificaciones, int SinLeer);
