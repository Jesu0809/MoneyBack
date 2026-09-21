using MoneyBack.Api.Models;

namespace MoneyBack.Api.Models.Tarjetas;

/// <summary>
/// Estrictamente privada del Usuario dueño — como Categoria, una tarjeta de
/// crédito es individual aunque el usuario tenga un Hogar compartido.
///
/// A propósito NO guarda un "saldo" como columna: se calcula en el endpoint
/// como SUMA(gastos etiquetados con esta tarjeta) - SUMA(PagoTarjeta), igual
/// que MetaAhorro.MontoActual se deriva de sus Movimientos en vez de
/// guardarse. Un valor guardado se puede desincronizar; uno calculado, no.
/// </summary>
public class TarjetaCredito
{
    public int Id { get; set; }

    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public string Nombre { get; set; } = string.Empty;

    /// <summary>
    /// Día del mes en que cierra el ciclo de facturación. Es metadato para
    /// un futuro recordatorio ("tu tarjeta cierra en 3 días") — no
    /// interviene en el cálculo del saldo pendiente, que es un balance
    /// corrido sin ventanas de fecha (ver TarjetasCreditoEndpoints).
    /// </summary>
    public int DiaCorte { get; set; }

    public bool Activa { get; set; } = true;

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
}
