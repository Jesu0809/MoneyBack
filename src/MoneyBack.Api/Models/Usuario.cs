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
