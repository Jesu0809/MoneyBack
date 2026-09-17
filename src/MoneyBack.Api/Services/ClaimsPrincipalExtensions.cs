using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace MoneyBack.Api.Services;

public static class ClaimsPrincipalExtensions
{
    public static int GetUsuarioId(this ClaimsPrincipal principal)
    {
        var valor = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                    ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (valor is null || !int.TryParse(valor, out var id))
        {
            throw new InvalidOperationException("El token no contiene un id de usuario válido.");
        }

        return id;
    }
}
