using MoneyBack.Api.Models;
using MoneyBack.Api.Models.Tarjetas;

namespace MoneyBack.Api.Models.DiaADia;

/// <summary>
/// Un ingreso o gasto personal. El Tipo real lo determina la Categoria
/// (evita que un movimiento y su categoría queden inconsistentes entre sí).
/// </summary>
public class MovimientoDiaADia
{
    public int Id { get; set; }

    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public int CategoriaId { get; set; }
    public Categoria Categoria { get; set; } = null!;

    /// <summary>Siempre positivo.</summary>
    public decimal Monto { get; set; }

    public DateTime Fecha { get; set; } = DateTime.UtcNow;

    public string? Nota { get; set; }

    /// <summary>
    /// El comercio tal como lo informó el banco, cuando el movimiento entró
    /// por el atajo. Vive aparte de la Nota a propósito: la nota es texto
    /// libre que la persona puede reescribir ("almuerzo con Ana"), mientras
    /// que esto es un dato del banco que no cambia. Separarlos es lo que
    /// permite que la app aprenda dónde va cada comercio sin confundir una
    /// anotación personal con el nombre de un sitio, y que seguir aprendiendo
    /// funcione aunque la persona le cambie la nota al gasto.
    ///
    /// null = el movimiento se registró a mano.
    /// </summary>
    public string? Comercio { get; set; }

    /// <summary>
    /// true si este gasto ya generó su aporte de redondeo automático a las
    /// metas del hogar (evita duplicar el aporte si el registro se vuelve
    /// a procesar).
    /// </summary>
    public bool RedondeoAplicado { get; set; } = false;

    /// <summary>
    /// Etiqueta opcional: con qué tarjeta se pagó este gasto. A propósito
    /// NO cambia en nada cómo este gasto afecta el saldo acumulado, las
    /// categorías, ni el redondeo — es solo informativa, para poder
    /// calcular el saldo pendiente de la tarjeta (ver TarjetasCreditoEndpoints).
    /// </summary>
    public int? TarjetaCreditoId { get; set; }
    public TarjetaCredito? TarjetaCredito { get; set; }
}
