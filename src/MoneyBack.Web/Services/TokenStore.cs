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

    public string? AccessToken { get; private set; }
    public DateTime? AccessTokenExpiraEn { get; private set; }
    public ClaimsPrincipal CurrentPrincipal { get; private set; } = new(new ClaimsIdentity());

    public event Action? OnChange;

    public bool EstaAutenticado => AccessToken is not null;

    public void SetSesion(AuthResponse auth)
    {
        AccessToken = auth.AccessToken;
        AccessTokenExpiraEn = auth.AccessTokenExpiraEn;
        CurrentPrincipal = new ClaimsPrincipal(new ClaimsIdentity(JwtParser.ParseClaims(auth.AccessToken), "jwt"));
        OnChange?.Invoke();
    }

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
        CurrentPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        await js.InvokeVoidAsync("localStorage.removeItem", RefreshTokenKey);
        OnChange?.Invoke();
    }
}
