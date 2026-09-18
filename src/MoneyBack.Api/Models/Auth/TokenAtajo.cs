namespace MoneyBack.Api.Models.Auth;

/// <summary>
/// Token de larga duración para automatizaciones (Atajos de iOS), separado
/// del JWT/RefreshToken de sesión normal a propósito: no rota, no expira
/// solo, y los endpoints que lo aceptan (Endpoints/AtajosEndpoints.cs) están
/// deliberadamente limitados a leer categorías y crear un movimiento — ni
/// siquiera con el token en mano se puede tocar metas, deudas, ni nada del
/// hogar. Igual que RefreshToken, nunca se guarda en texto plano, solo su
/// hash (ver TokenService.HashearToken, reutilizado tal cual).
/// </summary>
public class TokenAtajo
{
    public int Id { get; set; }

    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public string TokenHash { get; set; } = string.Empty;

    public string? Nombre { get; set; }

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public DateTime? UltimoUso { get; set; }
}
