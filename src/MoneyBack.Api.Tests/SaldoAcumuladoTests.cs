using System.Net.Http.Json;
using MoneyBack.Api.Dtos;
using MoneyBack.Api.Models.DiaADia;

namespace MoneyBack.Api.Tests;

public class SaldoAcumuladoTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public SaldoAcumuladoTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Balance_EsIngresosMenosGastos_SinImportarElOrdenDeRegistro()
    {
        var (cliente, _) = await _factory.CrearClienteAutenticadoAsync();
        var salario = await cliente.CrearCategoriaAsync("Salario", TipoCategoria.Ingreso);
        var mercado = await cliente.CrearCategoriaAsync("Mercado", TipoCategoria.Gasto);
        var arriendo = await cliente.CrearCategoriaAsync("Arriendo", TipoCategoria.Gasto);

        await cliente.RegistrarMovimientoAsync(salario.Id, 3_000_000m);
        await cliente.RegistrarMovimientoAsync(mercado.Id, 250_000m);
        await cliente.RegistrarMovimientoAsync(arriendo.Id, 1_200_000m);

        var resumen = await cliente.GetFromJsonAsync<ResumenDiaADiaResponse>("/api/movimientos-diaadia/resumen");

        Assert.Equal(3_000_000m, resumen!.TotalIngresos);
        Assert.Equal(1_450_000m, resumen.TotalGastos);
        Assert.Equal(1_550_000m, resumen.Balance);
    }

    [Fact]
    public async Task EliminarUnMovimiento_LoQuitaDelBalance()
    {
        var (cliente, _) = await _factory.CrearClienteAutenticadoAsync();
        var categoria = await cliente.CrearCategoriaAsync("Ocio", TipoCategoria.Gasto);

        var mov1 = await cliente.RegistrarMovimientoAsync(categoria.Id, 50_000m);
        await cliente.RegistrarMovimientoAsync(categoria.Id, 30_000m);

        await cliente.DeleteAsync($"/api/movimientos-diaadia/{mov1.Id}");

        var resumen = await cliente.GetFromJsonAsync<ResumenDiaADiaResponse>("/api/movimientos-diaadia/resumen");
        Assert.Equal(30_000m, resumen!.TotalGastos);
    }

    [Fact]
    public async Task DosUsuarios_TienenSaldosCompletamenteIndependientes()
    {
        var (clienteA, _) = await _factory.CrearClienteAutenticadoAsync();
        var (clienteB, _) = await _factory.CrearClienteAutenticadoAsync();

        var categoriaA = await clienteA.CrearCategoriaAsync("Gastos varios", TipoCategoria.Gasto);
        await clienteA.RegistrarMovimientoAsync(categoriaA.Id, 500_000m);

        var resumenB = await clienteB.GetFromJsonAsync<ResumenDiaADiaResponse>("/api/movimientos-diaadia/resumen");

        Assert.Equal(0m, resumenB!.TotalGastos);
        Assert.Equal(0m, resumenB.TotalIngresos);
    }

    [Fact]
    public async Task NoSePuedeRegistrarUnMontoNegativoOCero()
    {
        var (cliente, _) = await _factory.CrearClienteAutenticadoAsync();
        var categoria = await cliente.CrearCategoriaAsync("Mercado", TipoCategoria.Gasto);

        var respuestaCero = await cliente.PostAsJsonAsync("/api/movimientos-diaadia",
            new CrearMovimientoDiaADiaRequest(categoria.Id, 0m, null, null));
        var respuestaNegativo = await cliente.PostAsJsonAsync("/api/movimientos-diaadia",
            new CrearMovimientoDiaADiaRequest(categoria.Id, -10_000m, null, null));

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, respuestaCero.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, respuestaNegativo.StatusCode);
    }
}
