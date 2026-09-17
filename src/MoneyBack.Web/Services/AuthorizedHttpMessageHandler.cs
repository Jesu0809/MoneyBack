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
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (tokenStore.AccessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenStore.AccessToken);
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            var authService = serviceProvider.GetRequiredService<AuthService>();
            var refreshToken = await tokenStore.ObtenerRefreshTokenAsync();

            if (!string.IsNullOrEmpty(refreshToken) && await authService.IntentarRefrescarAsync(refreshToken))
            {
                var retryRequest = await CloneRequestAsync(request);
                retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenStore.AccessToken);
                response.Dispose();
                response = await base.SendAsync(retryRequest, cancellationToken);
            }
        }

        return response;
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
