using System.Security.Claims;
using MoneyBack.Api.Models.Metas;

namespace MoneyBack.Api.Services;

public static class HogarAuthorization
{
    /// <summary>
    /// Requiere que Miembros venga cargado (Include). Antes bastaba con
    /// comparar dos columnas del propio grupo; ahora la pertenencia vive en
    /// otra tabla, así que quien llame tiene que traerla.
    ///
    /// Se deja fallar en vez de devolver false cuando no está cargada: un
    /// false silencioso se vería como "no tienes permiso" y mandaría a
    /// buscar el error en el lugar equivocado.
    /// </summary>
    public static bool PerteneceAlHogar(this Hogar hogar, ClaimsPrincipal principal)
    {
        if (hogar.Miembros is null or { Count: 0 })
        {
            throw new InvalidOperationException(
                $"Los miembros del grupo {hogar.Id} no se cargaron. Falta un Include(h => h.Miembros).");
        }

        var usuarioId = principal.GetUsuarioId();
        return hogar.Miembros.Any(m => m.UsuarioId == usuarioId);
    }

    public static bool EsAdministrador(this Hogar hogar, ClaimsPrincipal principal)
    {
        var usuarioId = principal.GetUsuarioId();
        return hogar.Miembros.Any(m => m.UsuarioId == usuarioId && m.EsAdministrador);
    }
}
