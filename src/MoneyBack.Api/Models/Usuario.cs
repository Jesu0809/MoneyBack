using Microsoft.AspNetCore.Identity;

namespace MoneyBack.Api.Models;

/// <summary>
/// Extiende IdentityUser para heredar hash de contraseña, lockout,
/// security stamp, etc. de ASP.NET Core Identity en vez de reinventarlos.
/// Email/UserName ya vienen de IdentityUser; Nombre es lo único propio.
/// </summary>
public class Usuario : IdentityUser<int>
{
    public string Nombre { get; set; } = string.Empty;

    /// <summary>
    /// La meta a la que apunta el botón de aportar del día a día.
    ///
    /// Es de cada persona y no del grupo a propósito: dos personas del mismo
    /// hogar pueden estar empujando cosas distintas —uno el apartamento, el
    /// otro el fondo de emergencia— y con varios grupos la diferencia crece.
    /// Que la app elija por uno, o que pregunte cada vez, convierte un gesto
    /// de dos segundos en una decisión.
    ///
    /// null = todavía no ha elegido; ahí la app decide sola con lo que haya.
    /// </summary>
    public int? MetaFavoritaId { get; set; }

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Días aproximados del mes en que suele recibir ingresos (ej. 1 y 15),
    /// configurados a mano por ahora — no se corren exactos por feriados y
    /// esas cosas, así que cualquier cálculo con esto se trata siempre como
    /// estimado. Usados por InsightsService para avisar "te faltan X días
    /// para tu próximo pago". Detectarlos solos del historial de Ingresos
    /// es una mejora real pero queda para después (ver plan).
    /// </summary>
    public int? DiaPago1 { get; set; }
    public int? DiaPago2 { get; set; }

    /// <summary>
    /// Marca de cuándo se envió el último push de resumen semanal, para que
    /// ResumenSemanalService no lo mande dos veces el mismo domingo.
    /// </summary>
    public DateTime? UltimoResumenEnviado { get; set; }
}
