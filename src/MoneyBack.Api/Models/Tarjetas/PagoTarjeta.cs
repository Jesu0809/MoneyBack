namespace MoneyBack.Api.Models.Tarjetas;

/// <summary>
/// Registro puro de conciliación: "ya le pagué al banco lo que debía por la
/// tarjeta". A propósito NUNCA crea un MovimientoDiaADia — las compras ya se
/// registraron como gasto normal cuando ocurrieron (y ya restaron el saldo
/// acumulado en ese momento); si el pago también restara, la misma plata se
/// contaría dos veces. Ver TarjetasCreditoEndpoints para la fórmula del
/// saldo pendiente que usa esto.
/// </summary>
public class PagoTarjeta
{
    public int Id { get; set; }

    public int TarjetaCreditoId { get; set; }
    public TarjetaCredito TarjetaCredito { get; set; } = null!;

    public decimal Monto { get; set; }

    public DateTime Fecha { get; set; } = DateTime.UtcNow;
}
