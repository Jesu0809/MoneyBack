using MoneyBack.Api.Models;

namespace MoneyBack.Api.Models.Deudas;

/// <summary>
/// Un pago de cuota. UsuarioId es quién lo registró — relevante en deudas
/// compartidas, donde cualquiera de los dos puede pagar. MovimientoDiaADiaId
/// no es una FK real (sin navegación): solo referencia informativa al gasto
/// que se creó en el día a día de quien pagó.
/// </summary>
public class PagoDeuda
{
    public int Id { get; set; }

    public int DeudaId { get; set; }
    public Deuda Deuda { get; set; } = null!;

    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public decimal Monto { get; set; }

    public DateTime Fecha { get; set; } = DateTime.UtcNow;

    public int? MovimientoDiaADiaId { get; set; }
}
