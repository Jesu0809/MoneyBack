using MoneyBack.Api.Models;

namespace MoneyBack.Api.Models.DiaADia;

/// <summary>
/// Límite mensual de gasto para una categoría. Un Presupuesto por
/// Usuario+Categoria+Mes+Anio — se reemplaza el del mes actual en vez de
/// acumular históricos duplicados.
/// </summary>
public class Presupuesto
{
    public int Id { get; set; }

    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public int CategoriaId { get; set; }
    public Categoria Categoria { get; set; } = null!;

    public decimal MontoLimite { get; set; }

    public int Mes { get; set; }
    public int Anio { get; set; }
}
