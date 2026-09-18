namespace MoneyBack.Api.Models.Suscripciones;

/// <summary>
/// Una instancia puntual de cobro de una Suscripcion (un período concreto)
/// esperando que el usuario confirme si de verdad se cobró. MovimientoDiaADiaId
/// no es una FK real (sin navegación) — solo referencia informativa al gasto
/// que se creó al confirmar, si aplica.
/// </summary>
public class ConfirmacionCobro
{
    public int Id { get; set; }

    public int SuscripcionId { get; set; }
    public Suscripcion Suscripcion { get; set; } = null!;

    public DateTime PeriodoCobro { get; set; }

    public EstadoConfirmacion Estado { get; set; } = EstadoConfirmacion.Pendiente;

    public int? MovimientoDiaADiaId { get; set; }

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public DateTime? FechaResolucion { get; set; }
}
