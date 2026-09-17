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
}
