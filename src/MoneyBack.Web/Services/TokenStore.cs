using System.Security.Claims;
using Microsoft.JSInterop;
using MoneyBack.Web.Models;

namespace MoneyBack.Web.Services;

/// <summary>
/// El access token vive solo en memoria (nunca en localStorage) para
/// reducir el riesgo de robo por XSS; el refresh token sí se persiste
/// en localStorage porque sin eso el usuario tendría que loguearse cada
/// vez que recarga la página, y esta es una app de uso diario.
/// </summary>
public class TokenStore(IJSRuntime js)
{
    private const string RefreshTokenKey = "moneyback.refreshToken";

    /// <summary>
    /// Los datos mínimos de quién está conectado, para poder abrir la app sin
    /// señal. NO incluye el access token —ese sigue viviendo solo en memoria—
    /// así que esto no sirve para pedirle nada al servidor: solo para saber a
    /// quién mostrarle lo que ya está guardado en este teléfono.
    /// </summary>
    private const string IdentidadKey = "moneyback.identidad";

    public string? AccessToken { get; private set; }
    public DateTime? AccessTokenExpiraEn { get; private set; }
    public ClaimsPrincipal CurrentPrincipal { get; private set; } = new(new ClaimsIdentity());

    public event Action? OnChange;

    /// <summary>
    /// true cuando hay sesión pero sin poder hablar con el servidor. La app
    /// funciona con lo guardado y encola lo que se registre.
    /// </summary>
    public bool EsSesionOffline { get; private set; }

    public bool EstaAutenticado => AccessToken is not null || EsSesionOffline;

    public void SetSesion(AuthResponse auth)
    {
        AccessToken = auth.AccessToken;
        AccessTokenExpiraEn = auth.AccessTokenExpiraEn;
        EsSesionOffline = false;

        var claims = JwtParser.ParseClaims(auth.AccessToken).ToList();
        CurrentPrincipal = new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt"));

        // Se guarda para poder reconstruir la sesión sin señal la próxima vez.
        _ = GuardarIdentidadAsync(claims.Select(c => new ClaimGuardado(c.Type, c.Value)).ToList());

        OnChange?.Invoke();
    }

    private async Task GuardarIdentidadAsync(List<ClaimGuardado> claims)
    {
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(claims);
            await js.InvokeVoidAsync("localStorage.setItem", IdentidadKey, json);
        }
        catch { }
    }

    /// <summary>
    /// Reconstruye la sesión con lo último que se supo de la persona, para
    /// que pueda abrir la app sin señal.
    ///
    /// Sin esto, abrir la app sin datos mandaba al login — y el login
    /// necesita servidor, así que no había forma de entrar. Toda la parte
    /// offline quedaba inalcanzable justo cuando hacía falta.
    ///
    /// No es un agujero de seguridad: no hay access token, así que el
    /// servidor no le va a responder nada a esta sesión. Solo da acceso a lo
    /// que ya está guardado en este teléfono, que es de quien tiene el
    /// teléfono de todos modos.
    /// </summary>
    public async Task<bool> RestaurarSesionOfflineAsync()
    {
        try
        {
            var json = await js.InvokeAsync<string?>("localStorage.getItem", IdentidadKey);
            if (string.IsNullOrEmpty(json)) return false;

            var claims = System.Text.Json.JsonSerializer.Deserialize<List<ClaimGuardado>>(json);
            if (claims is null || claims.Count == 0) return false;

            CurrentPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
                claims.Select(c => new Claim(c.Tipo, c.Valor)), "offline"));
            EsSesionOffline = true;
            OnChange?.Invoke();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private record ClaimGuardado(string Tipo, string Valor);

    public async Task GuardarRefreshTokenAsync(string refreshToken)
    {
        await js.InvokeVoidAsync("localStorage.setItem", RefreshTokenKey, refreshToken);
    }

    public async Task<string?> ObtenerRefreshTokenAsync()
    {
        return await js.InvokeAsync<string?>("localStorage.getItem", RefreshTokenKey);
    }

    public async Task LimpiarAsync()
    {
        AccessToken = null;
        AccessTokenExpiraEn = null;
        EsSesionOffline = false;
        CurrentPrincipal = new ClaimsPrincipal(new ClaimsIdentity());

        try
        {
            await js.InvokeVoidAsync("localStorage.removeItem", RefreshTokenKey);
            await js.InvokeVoidAsync("localStorage.removeItem", IdentidadKey);
        }
        catch { }

        OnChange?.Invoke();
    }
}
