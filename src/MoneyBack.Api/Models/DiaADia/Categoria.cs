using MoneyBack.Api.Models;

namespace MoneyBack.Api.Models.DiaADia;

/// <summary>
/// Estrictamente privada del Usuario dueño — el día a día nunca se cruza
/// con el Hogar ni con la pareja, a diferencia de MetaAhorro.
/// </summary>
public class Categoria
{
    public int Id { get; set; }

    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public string Nombre { get; set; } = string.Empty;

    public TipoCategoria Tipo { get; set; }

    public string Icono { get; set; } = "📦";

    /// <summary>
    /// Permite "archivar" una categoría sin romper el historial de
    /// movimientos que ya la usaron.
    /// </summary>
    public bool Activa { get; set; } = true;

    public ICollection<MovimientoDiaADia> Movimientos { get; set; } = new List<MovimientoDiaADia>();
}
