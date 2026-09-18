namespace MoneyBack.Api.Models.Suscripciones;

/// <summary>
/// Cada cuánto se repite el cobro de una suscripción.
/// </summary>
public enum FrecuenciaSuscripcion
{
    Semanal,
    Mensual,
    Anual
}

/// <summary>
/// Estado de una instancia puntual de cobro (un período específico de una
/// Suscripcion) mientras espera que el usuario confirme si de verdad se
/// cobró o no. Nunca se crea un gasto sin pasar por Confirmado.
/// </summary>
public enum EstadoConfirmacion
{
    Pendiente,
    Confirmado,
    Rechazado
}
