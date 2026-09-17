using MoneyBack.Api.Models;

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
    /// true si este gasto ya generó su aporte de redondeo automático a las
    /// metas del hogar (evita duplicar el aporte si el registro se vuelve
    /// a procesar).
    /// </summary>
    public bool RedondeoAplicado { get; set; } = false;
}
