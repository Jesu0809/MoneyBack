using System.Net;

namespace MoneyBack.Web.Services;

/// <summary>
/// Convierte un fallo de red en una respuesta 503 normal, en vez de dejar
/// que la excepción suba.
///
/// Sin esto, cuando el API no contesta —está reiniciando, no hay señal, se
/// cayó la conexión a mitad de camino— HttpClient lanza una excepción que
/// nadie atrapa. Blazor la trata como error no manejado: sale la barra roja
/// de "An unhandled error has occurred", el spinner del botón se queda
/// girando para siempre porque el código que lo apaga nunca corre, y la
/// única salida es recargar. Le pasó a alguien intentando entrar durante
/// tres horas sin saber que solo tenía que esperar.
///
/// Con esto, cada llamada recibe una respuesta fallida como cualquier otra
/// y el código de siempre muestra un mensaje que se entiende.
/// </summary>
public class FallosDeRedHandler(EstadoConexion estado) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            var respuesta = await base.SendAsync(request, cancellationToken);

            // Cualquier respuesta del servidor, aunque sea un error suyo,
            // significa que hay camino hasta él.
            estado.MarcarEnLinea();
            return respuesta;
        }
        catch (HttpRequestException)
        {
            estado.MarcarSinConexion();
            return SinConexion("No pudimos conectarnos. Revisa tu conexión e inténtalo de nuevo.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            estado.MarcarSinConexion();
            // Se agotó el tiempo de espera. Se distingue del caso anterior
            // porque el consejo es distinto: acá sí llegó a haber conexión.
            return SinConexion("El servidor está tardando en responder. Inténtalo en un momento.");
        }
    }

    private static HttpResponseMessage SinConexion(string mensaje) =>
        new(HttpStatusCode.ServiceUnavailable)
        {
            // El formato que ya lee ApiResult para sacar el mensaje de error,
            // así que esto se muestra igual que cualquier otro fallo del API.
            Content = new StringContent(
                $$"""{"title":{{System.Text.Json.JsonSerializer.Serialize(mensaje)}}}""",
                System.Text.Encoding.UTF8,
                "application/problem+json")
        };
}
