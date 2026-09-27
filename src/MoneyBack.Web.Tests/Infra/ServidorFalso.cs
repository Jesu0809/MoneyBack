using System.Net;
using System.Text;
using System.Text.Json;
using MoneyBack.Web.Services;

namespace MoneyBack.Web.Tests.Infra;

/// <summary>
/// Un servidor de mentira que contesta lo que la prueba le diga.
///
/// La alternativa era simular <c>ApiClient</c> con una interfaz, pero entonces
/// las pruebas no tocarían la deserialización — y varios errores reales de
/// esta app vivían justo ahí (un campo con otro nombre, una lista que llega
/// null). Acá la respuesta se serializa a JSON de verdad y ApiClient la lee de
/// verdad; lo único falso es el cable.
/// </summary>
public class ServidorFalso : HttpMessageHandler, IHttpClientFactory
{
    /// <summary>
    /// El mismo manejador que envuelve las peticiones en producción. Va acá
    /// porque sin él las pruebas veían excepciones de red crudas, que es
    /// justo lo que ese manejador existe para que no pase — o sea, probaban
    /// un camino que en la app real no ocurre.
    /// </summary>
    public required EstadoConexion Estado { get; init; }

    private static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web);

    private readonly List<(string Metodo, string Ruta, Func<HttpRequestMessage, HttpResponseMessage> Responder)> _rutas = [];

    /// <summary>Todo lo que la interfaz le pidió al servidor, en orden.</summary>
    public List<(string Metodo, string Ruta, string? Cuerpo)> Llamadas { get; } = [];

    public ServidorFalso Responde(string metodo, string ruta, object? cuerpo, HttpStatusCode codigo = HttpStatusCode.OK)
    {
        _rutas.Add((metodo, ruta, _ => Json(cuerpo, codigo)));
        return this;
    }

    public ServidorFalso Falla(string metodo, string ruta, HttpStatusCode codigo, string titulo)
    {
        _rutas.Add((metodo, ruta, _ => Json(new { title = titulo, status = (int)codigo }, codigo)));
        return this;
    }

    /// <summary>
    /// Para lo que no se puede expresar con una respuesta fija: contar
    /// llamadas, cambiar la respuesta en la segunda, etc.
    /// </summary>
    public ServidorFalso RespondeCon(string metodo, string ruta, Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _rutas.Add((metodo, ruta, responder));
        return this;
    }

    private static HttpResponseMessage Json(object? cuerpo, HttpStatusCode codigo) => new(codigo)
    {
        Content = new StringContent(JsonSerializer.Serialize(cuerpo, Opciones), Encoding.UTF8, "application/json")
    };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var ruta = request.RequestUri!.AbsolutePath.TrimStart('/');
        var metodo = request.Method.Method;
        var cuerpo = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Llamadas.Add((metodo, ruta, cuerpo));

        // La última coincidencia gana, para que una prueba pueda pisar lo que
        // puso el andamiaje común sin tener que desarmarlo.
        for (var i = _rutas.Count - 1; i >= 0; i--)
        {
            var (m, r, responder) = _rutas[i];
            if (m == metodo && Coincide(r, ruta)) return responder(request);
        }

        // Un 404 silencioso haría que la pantalla se vea "vacía pero bien" y la
        // prueba pasaría por la razón equivocada. Mejor que reviente con el
        // nombre de la ruta que faltó registrar.
        throw new InvalidOperationException(
            $"La pantalla llamó a {metodo} /{ruta} y la prueba no dijo qué contestar.");
    }

    /// <summary>Soporta comodines: "api/metas/*" cubre "api/metas/7".</summary>
    private static bool Coincide(string patron, string ruta)
    {
        if (!patron.Contains('*')) return string.Equals(patron, ruta, StringComparison.OrdinalIgnoreCase);
        var partesPatron = patron.Split('/');
        var partesRuta = ruta.Split('/');
        if (partesPatron.Length != partesRuta.Length) return false;
        return !partesPatron.Where((p, i) => p != "*" && !string.Equals(p, partesRuta[i], StringComparison.OrdinalIgnoreCase)).Any();
    }

    HttpClient IHttpClientFactory.CreateClient(string name) =>
        new(new FallosDeRedHandler(Estado) { InnerHandler = this }, disposeHandler: false)
        {
            BaseAddress = new Uri("https://api.prueba/")
        };
}
