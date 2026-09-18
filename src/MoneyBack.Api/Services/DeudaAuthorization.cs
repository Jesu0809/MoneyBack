using System.Security.Claims;
using MoneyBack.Api.Models.Deudas;

namespace MoneyBack.Api.Services;

public static class DeudaAuthorization
{
    public static bool PerteneceALaDeuda(this Deuda deuda, ClaimsPrincipal principal) =>
        deuda.TipoPropiedad == TipoPropiedadDeuda.Privada
            ? deuda.UsuarioId == principal.GetUsuarioId()
            : deuda.Hogar!.PerteneceAlHogar(principal);
}
