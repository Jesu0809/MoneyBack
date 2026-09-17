using MoneyBack.Api.Models;

namespace MoneyBack.Api.Models.Metas;

/// <summary>
/// Un aporte o retiro individual sobre una MetaAhorro. Guardar quién
/// hizo cada movimiento es lo que permite mostrar "tú llevas $X, tu
/// pareja lleva $Y" sin mezclar esto con sus cuentas de día a día.
/// </summary>
public class MovimientoMeta
{
    public int Id { get; set; }

    public int MetaAhorroId { get; set; }
    public MetaAhorro MetaAhorro { get; set; } = null!;

    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public TipoMovimiento Tipo { get; set; }

    /// <summary>Siempre positivo; el Tipo determina si suma o resta.</summary>
    public decimal Monto { get; set; }

    public DateTime Fecha { get; set; } = DateTime.UtcNow;

    public string? Nota { get; set; }

    /// <summary>
    /// true si este movimiento vino del redondeo automático del día a día,
    /// false si fue un aporte manual.
    /// </summary>
    public bool EsAutomatico { get; set; } = false;
}
