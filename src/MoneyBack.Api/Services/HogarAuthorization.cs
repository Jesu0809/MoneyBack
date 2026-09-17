using System.Security.Claims;
using MoneyBack.Api.Models.Metas;

namespace MoneyBack.Api.Services;

public static class HogarAuthorization
{
    public static bool PerteneceAlHogar(this Hogar hogar, ClaimsPrincipal principal)
    {
        var usuarioId = principal.GetUsuarioId();
        return hogar.Usuario1Id == usuarioId || hogar.Usuario2Id == usuarioId;
    }
}
