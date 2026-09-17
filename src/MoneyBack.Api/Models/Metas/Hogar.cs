using MoneyBack.Api.Models;

namespace MoneyBack.Api.Models.Metas;

/// <summary>
/// Representa a la pareja como unidad. Es el único punto donde los dos
/// Usuario se cruzan — las cuentas de día a día (gastos/ingresos/categorías)
/// NO pasan por aquí, siguen asociadas 1:1 a cada Usuario por separado.
/// </summary>
public class Hogar
{
    public int Id { get; set; }

    public int Usuario1Id { get; set; }
    public Usuario Usuario1 { get; set; } = null!;

    public int Usuario2Id { get; set; }
    public Usuario Usuario2 { get; set; } = null!;

    /// <summary>
    /// % del redondeo automático del día a día que va al fondo de emergencia.
    /// Debe sumar 100 junto con PorcentajeRedondeoApartamento.
    /// </summary>
    public decimal PorcentajeRedondeoEmergencia { get; set; } = 20;

    /// <summary>
    /// % del redondeo automático del día a día que va al fondo del apartamento.
    /// </summary>
    public decimal PorcentajeRedondeoApartamento { get; set; } = 80;

    public ICollection<MetaAhorro> Metas { get; set; } = new List<MetaAhorro>();
}
