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

    /// <summary>
    /// Un solo refresco a la vez. El API rota el token en cada refresco, así
    /// que dos intentos en paralelo presentan el mismo token viejo y el
    /// segundo parece un reuso — justo lo que hay que evitar. Acá se
    /// coordinan los dos caminos que refrescan: el arranque de la app y el
    /// reintento tras un 401.
    /// </summary>
    private static readonly SemaphoreSlim UnSoloRefresco = new(1, 1);

    public async Task<bool> IntentarRefrescarAsync(string refreshToken)
    {
        await UnSoloRefresco.WaitAsync();
        try
        {
            // Si otro camino ya refrescó mientras esperábamos el turno, el
            // token guardado cambió y volver a pedir con el viejo sería
            // exactamente el reuso que dispara la revocación.
            var vigente = await tokenStore.ObtenerRefreshTokenAsync();
            if (vigente != refreshToken && tokenStore.EstaAutenticado) return true;

            var response = await Anon.PostAsJsonAsync("api/auth/refresh", new RefrescarTokenRequest(refreshToken));

            if (response.IsSuccessStatusCode)
            {
                var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
                tokenStore.SetSesion(auth);
                await tokenStore.GuardarRefreshTokenAsync(auth.RefreshToken);
                return true;
            }

            // Solo se borra la sesión cuando el servidor dice explícitamente
            // que el token ya no sirve. Antes se borraba ante CUALQUIER
            // respuesta fallida, y desde que los errores de red se convierten
            // en un 503 normal eso significaba cerrar la sesión cada vez que
            // la app abría sin señal o el servidor estaba reiniciando. El
            // token seguía siendo válido; lo botábamos nosotros.
            if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized
                or System.Net.HttpStatusCode.Forbidden)
            {
                await tokenStore.LimpiarAsync();
            }

            return false;
        }
        catch
        {
            // Ante la duda, conservar la sesión: un token que quizá servía es
            // mejor que obligar a escribir la contraseña de nuevo.
            return false;
        }
        finally
        {
            UnSoloRefresco.Release();
        }
    }

    /// <summary>
    /// Fuerza un refresh de token para que los claims (ej. nombre) queden al
    /// día tras editar el perfil, sin esperar a que expire el access token.
    /// </summary>
    public async Task RefrescarClaimsAsync()
    {
        var refreshToken = await tokenStore.ObtenerRefreshTokenAsync();
        if (!string.IsNullOrEmpty(refreshToken))
        {
            await IntentarRefrescarAsync(refreshToken);
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
