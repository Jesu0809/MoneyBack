using System.Net.Http.Json;
using MoneyBack.Web.Models;

namespace MoneyBack.Web.Services;

public class AuthService(IHttpClientFactory httpClientFactory, TokenStore tokenStore)
{
    private HttpClient Anon => httpClientFactory.CreateClient("ApiAnon");

    public async Task InicializarAsync()
    {
        var refreshToken = await tokenStore.ObtenerRefreshTokenAsync();
        if (string.IsNullOrEmpty(refreshToken)) return;

        await IntentarRefrescarAsync(refreshToken);
    }

    public async Task<ApiResult<AuthResponse>> RegistrarAsync(RegistrarUsuarioRequest request)
    {
        var response = await Anon.PostAsJsonAsync("api/auth/register", request);
        return await ApiResult<AuthResponse>.FromResponseAsync(response, async r =>
        {
            var auth = (await r.Content.ReadFromJsonAsync<AuthResponse>())!;
            tokenStore.SetSesion(auth);
            await tokenStore.GuardarRefreshTokenAsync(auth.RefreshToken);
            return auth;
        });
    }

    public async Task<ApiResult<AuthResponse>> LoginAsync(LoginRequest request)
    {
        var response = await Anon.PostAsJsonAsync("api/auth/login", request);
        return await ApiResult<AuthResponse>.FromResponseAsync(response, async r =>
        {
            var auth = (await r.Content.ReadFromJsonAsync<AuthResponse>())!;
            tokenStore.SetSesion(auth);
            await tokenStore.GuardarRefreshTokenAsync(auth.RefreshToken);
            return auth;
        });
    }

    public async Task<bool> IntentarRefrescarAsync(string refreshToken)
    {
        try
        {
            var response = await Anon.PostAsJsonAsync("api/auth/refresh", new RefrescarTokenRequest(refreshToken));
            if (!response.IsSuccessStatusCode)
            {
                await tokenStore.LimpiarAsync();
                return false;
            }

            var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
            tokenStore.SetSesion(auth);
            await tokenStore.GuardarRefreshTokenAsync(auth.RefreshToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task LogoutAsync()
    {
        var refreshToken = await tokenStore.ObtenerRefreshTokenAsync();
        if (!string.IsNullOrEmpty(refreshToken))
        {
            try
            {
                await Anon.PostAsJsonAsync("api/auth/logout", new RefrescarTokenRequest(refreshToken));
            }
            catch
            {
                // Si el logout remoto falla, igual limpiamos la sesión local.
            }
        }

        await tokenStore.LimpiarAsync();
    }
}
