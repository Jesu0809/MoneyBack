using MoneyBack.Api.Models;

namespace MoneyBack.Api.Models.Auth;

/// <summary>
/// Nunca se guarda el refresh token en texto plano — solo su hash. Rotación:
/// cada uso genera un token nuevo y marca este como reemplazado; si alguien
/// reintenta usar uno ya reemplazado, es señal de robo y se revoca la cadena.
/// </summary>
public class RefreshToken
{
    public int Id { get; set; }

    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public string TokenHash { get; set; } = string.Empty;

    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public DateTime ExpiraEn { get; set; }

    public DateTime? RevocadoEn { get; set; }
    public string? ReemplazadoPorTokenHash { get; set; }

    public bool EstaActivo => RevocadoEn is null && DateTime.UtcNow < ExpiraEn;
}
