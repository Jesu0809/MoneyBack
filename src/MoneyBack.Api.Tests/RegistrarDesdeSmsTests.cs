using System.Net.Http.Json;
using MoneyBack.Api.Dtos;
using MoneyBack.Api.Models.DiaADia;

namespace MoneyBack.Api.Tests;

/// <summary>
/// El atajo tiene una sola oportunidad: si el API no contesta en ese
/// instante, el gasto no se registra nunca y nadie se entera hasta que
/// cuadra cuentas y no le da. Esto es el rescate — pegar los mensajes del
/// banco y que entren todos.
/// </summary>
public class RegistrarDesdeSmsTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public RegistrarDesdeSmsTests(ApiFactory factory) => _factory = factory;

    private const string Pegado = """
        DAVIbank: Realizaste transaccion en BOLD SA*TESORO S por 64,000 con tu
        tarjeta Clasica 2026/09/26 14:40:29
        DAVIbank: Realizaste transaccion en OXXO CALLE 85 por 6,250 con tu
        tarjeta Clasica 2026/09/26 17:29:19
        DAVIbank: Enviaste 10,000 a la llave 3186014188 de manera exitosa el
        26-09-2026 a las 18:18:46.
        """;

    private async Task<HttpClient> PrepararAsync()
    {
        var (cliente, _) = await _factory.CrearClienteAutenticadoAsync();
        await cliente.CrearCategoriaAsync(CategoriasPredefinidas.SinClasificar, TipoCategoria.Gasto);
        return cliente;
    }

    private static async Task<RegistroDesdeSmsResponse> PegarAsync(HttpClient cliente, string texto)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/movimientos-diaadia/desde-sms", new TextoBancoRequest(texto));
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<RegistroDesdeSmsResponse>())!;
    }

    /// <summary>
    /// Al copiar desde Mensajes cada SMS viene partido en varias líneas, así
    /// que cortar por salto de línea rompería los mensajes por la mitad.
    /// </summary>
    [Fact]
    public async Task PegarVariosMensajesDeUnaVez_LosRegistraTodos()
    {
        var cliente = await PrepararAsync();

        var resultado = await PegarAsync(cliente, Pegado);

        Assert.Equal(3, resultado.Registrados);

        var movimientos = (await cliente.GetFromJsonAsync<List<MovimientoDiaADiaResponse>>("/api/movimientos-diaadia"))!;
        Assert.Equal([6_250m, 10_000m, 64_000m], movimientos.Select(m => m.Monto).OrderBy(m => m));
        Assert.Contains(movimientos, m => m.Comercio == "OXXO CALLE 85");
    }

    /// <summary>
    /// Puede que el atajo sí alcanzara a registrar alguno y la persona pegue
    /// todos por si acaso. Cobrar dos veces lo mismo destruye la confianza en
    /// los números mucho más que perder un gasto.
    /// </summary>
    [Fact]
    public async Task NoRegistraDosVecesElMismoGasto()
    {
        var cliente = await PrepararAsync();

        await PegarAsync(cliente, Pegado);
        var segunda = await PegarAsync(cliente, Pegado);

        Assert.Equal(0, segunda.Registrados);
        Assert.All(segunda.Resultados, r => Assert.Equal("Ya estaba registrado.", r.Detalle));

        var movimientos = (await cliente.GetFromJsonAsync<List<MovimientoDiaADiaResponse>>("/api/movimientos-diaadia"))!;
        Assert.Equal(3, movimientos.Count);
    }

    /// <summary>
    /// Se informa mensaje por mensaje: quien pega cinco y ve que entraron
    /// tres necesita saber cuáles dos faltaron.
    /// </summary>
    [Fact]
    public async Task DiceCualNoPudoRegistrarYPorQue()
    {
        var cliente = await PrepararAsync();

        var resultado = await PegarAsync(cliente, """
            DAVIbank: Realizaste transaccion en OXXO CALLE 85 por 6,250 con tu tarjeta Clasica
            DAVIbank: Tu clave temporal es 4821 y vence en 5 minutos
            """);

        Assert.Equal(1, resultado.Registrados);

        var fallido = resultado.Resultados.Single(r => !r.Registrado);
        Assert.Contains("monto", fallido.Detalle!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnPegadoVacioNoHaceNada()
    {
        var cliente = await PrepararAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/movimientos-diaadia/desde-sms", new TextoBancoRequest("   "));

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    /// <summary>
    /// Lo pegado respeta lo aprendido igual que el atajo: si ya se enseñó
    /// dónde va OXXO, estos entran clasificados.
    /// </summary>
    [Fact]
    public async Task RespetaLosComerciosYaAprendidos()
    {
        var cliente = await PrepararAsync();
        var mercado = await cliente.CrearCategoriaAsync("Mercado", TipoCategoria.Gasto);

        await PegarAsync(cliente, "DAVIbank: Realizaste transaccion en OXXO CALLE 85 por 6,250 con tu tarjeta Clasica");
        var mov = (await cliente.GetFromJsonAsync<List<MovimientoDiaADiaResponse>>("/api/movimientos-diaadia"))!.Single();
        await cliente.PutAsJsonAsync($"/api/movimientos-diaadia/{mov.Id}",
            new ActualizarMovimientoDiaADiaRequest(mercado.Id, mov.Monto, null, mov.Nota));

        var resultado = await PegarAsync(cliente, "DAVIbank: Realizaste transaccion en OXXO CALLE 85 por 9,900 con tu tarjeta Clasica");

        Assert.Equal("Mercado", resultado.Resultados.Single().Detalle);
    }
}
