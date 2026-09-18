using MoneyBack.Api.Models.DiaADia;

namespace MoneyBack.Api.Models.Suscripciones;

/// <summary>
/// Un cobro programado personal (tipo Netflix). Nunca se registra un gasto
/// solo porque llegó la fecha — RevisionSuscripcionesService solo crea una
/// ConfirmacionCobro pendiente, y el gasto real se crea al resolverla.
/// </summary>
public class Suscripcion
{
    public int Id { get; set; }

    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public string Nombre { get; set; } = string.Empty;

    public decimal Monto { get; set; }

    /// <summary>
    /// Requerida: al confirmar un cobro hace falta una categoría válida
    /// para crear el gasto, igual que un movimiento manual del día a día.
    /// </summary>
    public int CategoriaId { get; set; }
    public Categoria Categoria { get; set; } = null!;

    public FrecuenciaSuscripcion Frecuencia { get; set; }

    /// <summary>
    /// Fecha del próximo cobro esperado. Se avanza automáticamente (por
    /// AddDays/AddMonths/AddYears según Frecuencia) cada vez que se resuelve
    /// una confirmación, sin importar si el usuario dijo que sí se cobró.
    /// </summary>
    public DateTime ProximoCobro { get; set; }

    /// <summary>
    /// Con cuántos días de anticipación debe aparecer en el panel/push.
    /// </summary>
    public int DiasAvisoPrevio { get; set; } = 3;

    public bool Activa { get; set; } = true;

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
}
