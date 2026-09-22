using System.Net.Http.Headers;

namespace MoneyBack.Web.Services;

/// <summary>
/// Agrega el access token a cada request y, si el API responde 401 (token
/// vencido), intenta UNA vez refrescar la sesión con el refresh token y
/// repite la request original — así el usuario nunca ve el vencimiento
/// mientras el refresh token siga vivo.
/// </summary>
public class AuthorizedHttpMessageHandler(TokenStore tokenStore, IServiceProvider serviceProvider) : DelegatingHandler
{
    /// <summary>
    /// Estático porque el handler se registra como Transient: cada request
    /// puede traer su propia instancia, así que un campo de instancia no
    /// coordinaría nada.
    ///
    /// Sin esto, varias requests en paralelo que vencen a la vez intentan
    /// refrescar cada una con el MISMO refresh token. El API rota el token en
    /// cada refresco y trata el reuso de uno ya rotado como posible robo:
    /// revoca toda la sesión (ver /api/auth/refresh). O sea, el usuario
    /// quedaría deslogueado justo por abrir la app. El semáforo deja pasar un
    /// solo refresco y los demás reutilizan el token nuevo.
    /// </summary>
    private static readonly SemaphoreSlim Refresco = new(1, 1);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (tokenStore.AccessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenStore.AccessToken);
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode != System.Net.HttpStatusCode.Unauthorized) return response;

        var tokenRechazado = tokenStore.AccessToken;

        await Refresco.WaitAsync(cancellationToken);
        try
        {
            // Si mientras esperábamos el turno otra request ya refrescó, el
            // token guardado cambió: no hay que refrescar de nuevo (eso sería
            // justo el reuso que dispara la revocación), solo reintentar.
            if (tokenStore.AccessToken == tokenRechazado)
            {
                var authService = serviceProvider.GetRequiredService<AuthService>();
                var refreshToken = await tokenStore.ObtenerRefreshTokenAsync();

                if (string.IsNullOrEmpty(refreshToken) || !await authService.IntentarRefrescarAsync(refreshToken))
                {
                    return response;
                }
            }
        }
        finally
        {
            Refresco.Release();
        }

        var retryRequest = await CloneRequestAsync(request);
        retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenStore.AccessToken);
        response.Dispose();
        return await base.SendAsync(retryRequest, cancellationToken);
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage original)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri);

        if (original.Content is not null)
        {
            var bytes = await original.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(bytes);
            foreach (var header in original.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        foreach (var header in original.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}
