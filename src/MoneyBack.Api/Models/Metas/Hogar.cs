using MoneyBack.Api.Models;

namespace MoneyBack.Api.Models.Metas;

/// <summary>
/// Un grupo de personas que ahorran juntas. Es el único punto donde varias
/// cuentas se cruzan — el día a día (gastos, ingresos, categorías) sigue
/// siendo de cada quien por separado.
///
/// Antes era "el hogar": exactamente dos personas, en dos columnas. Con eso
/// no se podía ahorrar en familia ni con amigos, y una misma persona no
/// podía tener un grupo con su pareja y otro con sus hermanos. El nombre de
/// la tabla se conserva para no reescribir media base; lo que cambió es que
/// los miembros viven aparte y pueden ser los que sean.
/// </summary>
public class Hogar
{
    public int Id { get; set; }

    /// <summary>
    /// Cómo lo llaman quienes están adentro: "Nosotros", "Los Nuncira",
    /// "Viaje con los del trabajo". Con un solo grupo daba igual; con varios
    /// es lo único que los distingue.
    /// </summary>
    public string Nombre { get; set; } = "Nuestro hogar";

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public ICollection<MiembroHogar> Miembros { get; set; } = new List<MiembroHogar>();

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
