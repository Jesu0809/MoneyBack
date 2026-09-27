using System.Net.Http.Json;
using MoneyBack.Api.Dtos;
using MoneyBack.Api.Models.Auth;
using MoneyBack.Api.Models.DiaADia;

namespace MoneyBack.Api.Tests;

/// <summary>
/// "El atajo no hizo nada" tenía dos explicaciones —la automatización no se
/// disparó, o sí llamó y falló algo en el servidor— y ninguna forma de
/// distinguirlas. iOS no muestra historial de automatizaciones, así que la
/// única evidencia posible es la de este lado.
/// </summary>
public class LlamadasAtajoTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public LlamadasAtajoTests(ApiFactory factory) => _factory = factory;

    private async Task<(HttpClient Cliente, string Token)> PrepararAsync()
    {
        var (cliente, _) = await _factory.CrearClienteAutenticadoAsync();
        await cliente.CrearCategoriaAsync(CategoriasPredefinidas.SinClasificar, TipoCategoria.Gasto);

        var respuesta = await cliente.PostAsJsonAsync("/api/tokens-atajo", new CrearTokenAtajoRequest("iPhone"));
        var token = (await respuesta.Content.ReadFromJsonAsync<TokenAtajoCreadoResponse>())!.Token;
        return (cliente, token);
    }

    private async Task LlamarAsync(string token, string texto)
    {
        var cliente = _factory.CreateClient();
        cliente.DefaultRequestHeaders.Add("X-Atajo-Token", token);
        cliente.DefaultRequestHeaders.Add("X-Categoria", CategoriasPredefinidas.SinClasificar);
        await cliente.PostAsync("/api/atajos/registrar-texto", new StringContent(texto));
    }

    private static Task<List<LlamadaAtajoResponse>?> HistorialAsync(HttpClient cliente) =>
        cliente.GetFromJsonAsync<List<LlamadaAtajoResponse>>("/api/tokens-atajo/llamadas");

    [Fact]
    public async Task UnaLlamadaQueFuncionaQuedaRegistrada()
    {
        var (cliente, token) = await PrepararAsync();

        await LlamarAsync(token, "DAVIbank: Realizaste transaccion en OXXO CALLE 85 por 6,250 con tu tarjeta Clasica");

        var llamada = Assert.Single((await HistorialAsync(cliente))!);
        Assert.True(llamada.Exito);
        Assert.Contains("6.250", llamada.Detalle);
        Assert.Contains("OXXO", llamada.Texto!);
    }

    /// <summary>
    /// Sobre todo las que fallan: un intento fallido registrado es la
    /// diferencia entre saber qué pasó y adivinar.
    /// </summary>
    [Fact]
    public async Task UnaLlamadaQueFallaTambienQuedaRegistrada_ConLaRazon()
    {
        var (cliente, token) = await PrepararAsync();

        await LlamarAsync(token, "DAVIbank: Tu clave temporal es 4821 y vence en 5 minutos");

        var llamada = Assert.Single((await HistorialAsync(cliente))!);
        Assert.False(llamada.Exito);
        Assert.False(string.IsNullOrWhiteSpace(llamada.Detalle));
    }

    /// <summary>
    /// Si no hay ninguna llamada a la hora de la compra, la automatización
    /// nunca se disparó y el problema está en el teléfono, no acá. Ese es
    /// justo el dato que hacía falta.
    /// </summary>
    [Fact]
    public async Task SinLlamadas_ElHistorialQuedaVacio()
    {
        var (cliente, _) = await PrepararAsync();

        Assert.Empty((await HistorialAsync(cliente))!);
    }

    [Fact]
    public async Task SoloSeGuardanLasUltimas_NoCreceSinControl()
    {
        var (cliente, token) = await PrepararAsync();

        for (var i = 1; i <= LlamadaAtajo.MaximoPorUsuario + 5; i++)
        {
            await LlamarAsync(token, $"DAVIbank: Realizaste transaccion en TIENDA por {i},500 con tu tarjeta Clasica");
        }

        Assert.Equal(LlamadaAtajo.MaximoPorUsuario, (await HistorialAsync(cliente))!.Count);
    }

    [Fact]
    public async Task NadieVeElHistorialDeOtraCuenta()
    {
        var (_, tokenA) = await PrepararAsync();
        var (clienteB, _) = await PrepararAsync();

        await LlamarAsync(tokenA, "DAVIbank: Realizaste transaccion en OXXO por 6,250 con tu tarjeta Clasica");

        Assert.Empty((await HistorialAsync(clienteB))!);
    }

    /// <summary>
    /// Las claves temporales del banco pasan por el mismo camino —no traen
    /// monto, así que quedan como intento fallido— y guardar una clave de
    /// acceso bancario para poder diagnosticar no es un intercambio
    /// aceptable, aunque expire en cinco minutos.
    /// </summary>
    [Fact]
    public async Task NoGuardaLasClavesTemporalesDelBanco()
    {
        var (cliente, token) = await PrepararAsync();

        await LlamarAsync(token, "DAVIbank: Tu clave temporal es 482193 y vence en 5 minutos");

        var llamada = Assert.Single((await HistorialAsync(cliente))!);
        Assert.DoesNotContain("482193", llamada.Texto!);
        Assert.Contains("clave temporal", llamada.Texto!);
    }

    /// <summary>
    /// Y no se lleva por delante los montos, que es justo lo que uno necesita
    /// ver en el historial.
    /// </summary>
    [Fact]
    public async Task ElMontoDeUnaCompraSiSeGuarda()
    {
        var (cliente, token) = await PrepararAsync();

        await LlamarAsync(token, "DAVIbank: Realizaste transaccion en OXXO CALLE 85 por 6,250 con tu tarjeta Clasica");

        var llamada = Assert.Single((await HistorialAsync(cliente))!);
        Assert.Contains("6,250", llamada.Texto!);
    }
}
