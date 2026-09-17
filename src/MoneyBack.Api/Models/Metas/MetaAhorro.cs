using System.ComponentModel.DataAnnotations.Schema;

namespace MoneyBack.Api.Models.Metas;

/// <summary>
/// Una meta de ahorro del hogar (Apartamento o Emergencia). El monto
/// acumulado NO se guarda como columna — se calcula sumando los
/// Movimientos, para que nunca se desincronice del historial real.
/// </summary>
public class MetaAhorro
{
    public int Id { get; set; }

    public int HogarId { get; set; }
    public Hogar Hogar { get; set; } = null!;

    public TipoMeta Tipo { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public decimal MontoObjetivo { get; set; }

    /// <summary>
    /// Fecha estimada para alcanzar la meta al ritmo de ahorro actual.
    /// Se recalcula periódicamente desde el simulador, no la edita el usuario a mano.
    /// </summary>
    public DateTime? FechaObjetivoEstimada { get; set; }

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Permite "archivar" una meta ya cumplida sin borrar su historial de movimientos.
    /// </summary>
    public bool Activa { get; set; } = true;

    public ICollection<MovimientoMeta> Movimientos { get; set; } = new List<MovimientoMeta>();

    /// <summary>
    /// Monto actual acumulado = suma de aportes − suma de retiros.
    /// No mapeado a la base de datos: siempre se deriva de Movimientos.
    /// Requiere que Movimientos esté cargado (Include) para calcular bien.
    /// </summary>
    [NotMapped]
    public decimal MontoActual =>
        Movimientos.Where(m => m.Tipo == TipoMovimiento.Aporte).Sum(m => m.Monto) -
        Movimientos.Where(m => m.Tipo == TipoMovimiento.Retiro).Sum(m => m.Monto);

    [NotMapped]
    public decimal PorcentajeCompletado =>
        MontoObjetivo <= 0 ? 0 : Math.Min(100, (MontoActual / MontoObjetivo) * 100);
}
