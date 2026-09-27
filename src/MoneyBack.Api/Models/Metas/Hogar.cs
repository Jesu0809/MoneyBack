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
    /// Interruptor general: si está apagado, ningún gasto del día a día de
    /// ningún miembro genera aporte automático a las metas. El reparto entre
    /// metas ya no vive acá sino en cada MetaAhorro, para que un grupo pueda
    /// tener las que quiera y no solo dos.
    /// </summary>
    public bool RedondeoActivo { get; set; } = false;

    /// <summary>
    /// true si el hogar busca vivienda en uno de los 45 municipios del
    /// Decreto 1467/2019 (Bogotá y su aglomeración, Cali, Medellín,
    /// Barranquilla, Bucaramanga y sus aglomeraciones) — tope VIS de 150
    /// SMMLV en vez de 135. Lo decide la ubicación objetivo del hogar, no
    /// una vivienda puntual, así que vive aquí y no en MetaAhorro.
    /// </summary>
    public bool AplicaTope150 { get; set; } = false;

    public ICollection<MetaAhorro> Metas { get; set; } = new List<MetaAhorro>();
}
