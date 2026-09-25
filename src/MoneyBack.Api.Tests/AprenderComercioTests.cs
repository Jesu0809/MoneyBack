using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using MoneyBack.Api.Data;
using MoneyBack.Api.Dtos;
using MoneyBack.Api.Models.DiaADia;

namespace MoneyBack.Api.Tests;

/// <summary>
/// El banco nombra el comercio pero nunca la categoría, y adivinarla por el
/// nombre falla: "MERCADO LIBRE" no es mercado. La salida es que la app
/// recuerde la decisión de la propia persona, para que corregir una vez sirva
/// para siempre — si corregir no rindiera, nadie corregiría.
/// </summary>
public class AprenderComercioTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AprenderComercioTests(ApiFactory factory) => _factory = factory;

    private async Task<(HttpClient Cliente, string Token, CategoriaResponse SinClasificar, CategoriaResponse Mercado)>
        PrepararAsync()
    {
        var (cliente, _) = await _factory.CrearClienteAutenticadoAsync();
        var sinClasificar = await cliente.CrearCategoriaAsync(CategoriasPredefinidas.SinClasificar, TipoCategoria.Gasto);
        var mercado = await cliente.CrearCategoriaAsync("Mercado", TipoCategoria.Gasto);

        var respuesta = await cliente.PostAsJsonAsync("/api/tokens-atajo", new CrearTokenAtajoRequest("pruebas"));
        var token = (await respuesta.Content.ReadFromJsonAsync<TokenAtajoCreadoResponse>())!.Token;

        return (cliente, token, sinClasificar, mercado);
    }

    private static async Task<string> RegistrarPorAtajoAsync(ApiFactory factory, string token, string texto)
    {
        var cliente = factory.CreateClient();
        cliente.DefaultRequestHeaders.Add("X-Atajo-Token", token);
        cliente.DefaultRequestHeaders.Add("X-Categoria", CategoriasPredefinidas.SinClasificar);

        var respuesta = await cliente.PostAsync("/api/atajos/registrar-texto", new StringContent(texto));
        return await respuesta.Content.ReadAsStringAsync();
    }

    [Fact]
    public async Task CorregirUnGasto_HaceQueElMismoComercioEntreYaClasificado()
    {
        var (cliente, token, _, mercado) = await PrepararAsync();
        const string sms = "DAVIbank: Realizaste  transaccion en OXXO CALLE 100 por 13,500 con tu tarjeta Clasica";

        // Primera compra: nadie sabe qué es OXXO, entra sin clasificar.
        var primera = await RegistrarPorAtajoAsync(_factory, token, sms);
        Assert.Contains(CategoriasPredefinidas.SinClasificar, primera);

        // La persona la corrige a Mercado: ahí está enseñando.
        var movimientos = await cliente.GetFromJsonAsync<List<MovimientoDiaADiaResponse>>("/api/movimientos-diaadia");
        var mov = movimientos!.Single();
        await cliente.PutAsJsonAsync($"/api/movimientos-diaadia/{mov.Id}",
            new ActualizarMovimientoDiaADiaRequest(mercado.Id, mov.Monto, null, mov.Nota));

        // Segunda compra en el mismo sitio: ya no pregunta.
        var segunda = await RegistrarPorAtajoAsync(_factory, token, sms);
        Assert.Contains("Mercado", segunda);
        Assert.DoesNotContain(CategoriasPredefinidas.SinClasificar, segunda);
    }

    [Fact]
    public async Task AlCorregir_ArrastraLosAnterioresDelMismoComercioQueSeguianSinClasificar()
    {
        var (cliente, token, _, mercado) = await PrepararAsync();
        const string sms = "DAVIbank: Realizaste  transaccion en OXXO CALLE 100 por 13,500 con tu tarjeta Clasica";

        // Tres compras en el mismo sitio antes de que nadie clasifique nada.
        for (var i = 0; i < 3; i++) await RegistrarPorAtajoAsync(_factory, token, sms);

        var movimientos = await cliente.GetFromJsonAsync<List<MovimientoDiaADiaResponse>>("/api/movimientos-diaadia");
        var primero = movimientos!.First();

        var respuesta = await cliente.PutAsJsonAsync($"/api/movimientos-diaadia/{primero.Id}",
            new ActualizarMovimientoDiaADiaRequest(mercado.Id, primero.Monto, null, primero.Nota));
        var resultado = (await respuesta.Content.ReadFromJsonAsync<MovimientoActualizadoResponse>())!;

        // Los otros dos se mueven solos: no tenían decisión que respetar.
        Assert.Equal(2, resultado.OtrosReclasificados);

        var despues = await cliente.GetFromJsonAsync<List<MovimientoDiaADiaResponse>>("/api/movimientos-diaadia");
        Assert.All(despues!, m => Assert.Equal("Mercado", m.CategoriaNombre));
    }

    /// <summary>
    /// Un gasto que la persona ya clasificó a mano no se toca, aunque sea del
    /// mismo comercio: a veces se compra el mercado y a veces un regalo en el
    /// mismo lugar, y pisar esa decisión sería peor que no aprender.
    /// </summary>
    [Fact]
    public async Task NoPisaLosGastosQueLaPersonaYaHabiaClasificado()
    {
        var (cliente, token, _, mercado) = await PrepararAsync();
        var ropa = await cliente.CrearCategoriaAsync("Ropa", TipoCategoria.Gasto);
        const string sms = "DAVIbank: Realizaste  transaccion en OXXO CALLE 100 por 13,500 con tu tarjeta Clasica";

        await RegistrarPorAtajoAsync(_factory, token, sms);
        await RegistrarPorAtajoAsync(_factory, token, sms);

        var movimientos = (await cliente.GetFromJsonAsync<List<MovimientoDiaADiaResponse>>("/api/movimientos-diaadia"))!;

        // El primero se marca a mano como Ropa.
        await cliente.PutAsJsonAsync($"/api/movimientos-diaadia/{movimientos[0].Id}",
            new ActualizarMovimientoDiaADiaRequest(ropa.Id, movimientos[0].Monto, null, movimientos[0].Nota));

        // El segundo se marca como Mercado: no debe arrastrar al primero.
        var respuesta = await cliente.PutAsJsonAsync($"/api/movimientos-diaadia/{movimientos[1].Id}",
            new ActualizarMovimientoDiaADiaRequest(mercado.Id, movimientos[1].Monto, null, movimientos[1].Nota));
        var resultado = (await respuesta.Content.ReadFromJsonAsync<MovimientoActualizadoResponse>())!;

        Assert.Equal(0, resultado.OtrosReclasificados);

        var despues = (await cliente.GetFromJsonAsync<List<MovimientoDiaADiaResponse>>("/api/movimientos-diaadia"))!;
        Assert.Contains(despues, m => m.CategoriaNombre == "Ropa");
        Assert.Contains(despues, m => m.CategoriaNombre == "Mercado");
    }

    [Fact]
    public async Task LoAprendidoEsDeCadaPersona_NoSeCruzaEntreCuentas()
    {
        var (clienteA, tokenA, _, mercadoA) = await PrepararAsync();
        var (_, tokenB, _, _) = await PrepararAsync();
        const string sms = "DAVIbank: Realizaste  transaccion en OXXO CALLE 100 por 13,500 con tu tarjeta Clasica";

        await RegistrarPorAtajoAsync(_factory, tokenA, sms);
        var mov = (await clienteA.GetFromJsonAsync<List<MovimientoDiaADiaResponse>>("/api/movimientos-diaadia"))!.Single();
        await clienteA.PutAsJsonAsync($"/api/movimientos-diaadia/{mov.Id}",
            new ActualizarMovimientoDiaADiaRequest(mercadoA.Id, mov.Monto, null, mov.Nota));

        // Para la otra cuenta, OXXO sigue siendo desconocido.
        var deB = await RegistrarPorAtajoAsync(_factory, tokenB, sms);
        Assert.Contains(CategoriasPredefinidas.SinClasificar, deB);
    }

    [Fact]
    public async Task CambiarSoloElMonto_NoEnseniaNada()
    {
        var (cliente, token, sinClasificar, _) = await PrepararAsync();
        await RegistrarPorAtajoAsync(_factory, token,
            "DAVIbank: Realizaste  transaccion en OXXO CALLE 100 por 13,500 con tu tarjeta Clasica");

        var mov = (await cliente.GetFromJsonAsync<List<MovimientoDiaADiaResponse>>("/api/movimientos-diaadia"))!.Single();
        await cliente.PutAsJsonAsync($"/api/movimientos-diaadia/{mov.Id}",
            new ActualizarMovimientoDiaADiaRequest(sinClasificar.Id, 99_000m, null, mov.Nota));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Empty(db.ComerciosCategoria.Where(c => c.Comercio.Contains("oxxo")));
    }
}
