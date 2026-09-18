using Microsoft.JSInterop;
using MoneyBack.Web.Models;

namespace MoneyBack.Web.Services;

/// <summary>
/// Orquesta activar/desactivar notificaciones push: pide permiso y se
/// suscribe en el navegador (wwwroot/js/interop.js) y sincroniza esa
/// suscripción con el backend (ApiClient). El permiso del navegador SOLO se
/// puede pedir a raíz de un gesto del usuario — por eso ActivarAsync se debe
/// llamar desde el @onclick de un botón, nunca automáticamente al cargar.
/// </summary>
public class PushNotificationService(IJSRuntime js, ApiClient api)
{
    private IJSObjectReference? _modulo;

    private async Task<IJSObjectReference> ObtenerModuloAsync()
    {
        _modulo ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/interop.js");
        return _modulo;
    }

    public async Task<bool> SonSoportadasAsync()
    {
        var modulo = await ObtenerModuloAsync();
        return await modulo.InvokeAsync<bool>("notificacionesSoportadas");
    }

    public async Task<string> ObtenerEstadoPermisoAsync()
    {
        var modulo = await ObtenerModuloAsync();
        return await modulo.InvokeAsync<string>("estadoPermisoNotificaciones");
    }

    public async Task<bool> EstaSuscritoAsync()
    {
        var modulo = await ObtenerModuloAsync();
        return await modulo.InvokeAsync<bool>("estaSuscritoNotificaciones");
    }

    public async Task<bool> ActivarAsync()
    {
        var vapidKey = await api.ObtenerVapidPublicKeyAsync();
        if (string.IsNullOrEmpty(vapidKey)) return false;

        var modulo = await ObtenerModuloAsync();
        var suscripcion = await modulo.InvokeAsync<SuscripcionJs?>("pedirPermisoYSuscribirse", vapidKey);
        if (suscripcion is null) return false;

        return await api.SuscribirsePushAsync(new SuscribirsePushRequest(suscripcion.Endpoint, suscripcion.P256dh, suscripcion.Auth));
    }

    public async Task DesactivarAsync()
    {
        var modulo = await ObtenerModuloAsync();
        var suscripcion = await modulo.InvokeAsync<SuscripcionJs?>("desuscribirseNotificaciones");
        if (suscripcion is not null)
        {
            await api.DesuscribirsePushAsync(new SuscribirsePushRequest(suscripcion.Endpoint, suscripcion.P256dh, suscripcion.Auth));
        }
    }

    private record SuscripcionJs(string Endpoint, string P256dh, string Auth);
}
