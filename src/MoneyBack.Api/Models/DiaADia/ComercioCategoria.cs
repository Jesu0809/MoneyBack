using MoneyBack.Api.Models;

namespace MoneyBack.Api.Models.DiaADia;

/// <summary>
/// Recuerda en qué categoría va cada comercio, aprendido de las correcciones
/// de la propia persona. El banco nombra el comercio ("OXXO CALLE 100") pero
/// nunca la categoría, y adivinarla por el nombre falla feo: "MERCADO LIBRE"
/// no es mercado y "SALUD TOTAL EPS" no es salud.
///
/// La primera vez el gasto entra Sin clasificar; cuando la persona lo corrige,
/// esa decisión queda guardada acá y el mismo comercio ya no vuelve a
/// preguntar. Es su criterio, no una suposición — por eso acierta siempre.
/// </summary>
public class ComercioCategoria
{
    public int Id { get; set; }

    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    /// <summary>
    /// Normalizado (minúsculas, sin tildes) para que "Éxito" y "EXITO" sean
    /// el mismo comercio. El nombre tal cual lo escribió el banco queda en la
    /// nota del movimiento.
    /// </summary>
    public string Comercio { get; set; } = string.Empty;

    public int CategoriaId { get; set; }
    public Categoria Categoria { get; set; } = null!;

    public DateTime FechaAprendido { get; set; } = DateTime.UtcNow;
}
