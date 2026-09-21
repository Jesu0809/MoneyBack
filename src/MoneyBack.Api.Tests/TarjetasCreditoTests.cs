using System.Net.Http.Json;
using MoneyBack.Api.Dtos;
using MoneyBack.Api.Models.DiaADia;

namespace MoneyBack.Api.Tests;

/// <summary>
/// Regresión directa del bug real que motivó esta feature: comprar con
/// tarjeta debe restar el saldo acumulado una sola vez, y pagarle al banco
/// (parcial o total) NUNCA debe volver a tocar ese saldo — solo baja lo
/// pendiente de la tarjeta.
/// </summary>
public class TarjetasCreditoTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public TarjetasCreditoTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task ComprarConTarjeta_RestaSaldoUnaSolaVez_YPagoParcialNoLoVuelveATocar()
    {
        var (cliente, _) = await _factory.CrearClienteAutenticadoAsync();

        var categoria = await cliente.CrearCategoriaAsync("Mercado", TipoCategoria.Gasto);
        var tarjetaResp = await cliente.PostAsJsonAsync("/api/tarjetas-credito", new CrearTarjetaCreditoRequest("Visa Test", 20));
        tarjetaResp.EnsureSuccessStatusCode();
        var tarjeta = (await tarjetaResp.Content.ReadFromJsonAsync<TarjetaCreditoResponse>())!;

        await cliente.RegistrarMovimientoAsync(categoria.Id, 150_000m, tarjetaCreditoId: tarjeta.Id);

        var saldoAcumuladoTrasCompra = await ObtenerBalanceAsync(cliente);
        Assert.Equal(-150_000m, saldoAcumuladoTrasCompra);

        var saldoPendienteTrasCompra = await ObtenerSaldoPendienteAsync(cliente, tarjeta.Id);
        Assert.Equal(150_000m, saldoPendienteTrasCompra.Monto);

        var pagoResp = await cliente.PostAsJsonAsync($"/api/tarjetas-credito/{tarjeta.Id}/pagos", new RegistrarPagoTarjetaRequest(50_000m));
        pagoResp.EnsureSuccessStatusCode();

        var saldoPendienteTrasPago = await ObtenerSaldoPendienteAsync(cliente, tarjeta.Id);
        Assert.Equal(100_000m, saldoPendienteTrasPago.Monto);

        // El punto central del bug original: pagar la tarjeta NO debe volver
        // a restar del saldo acumulado del día a día (ya se restó al comprar).
        var saldoAcumuladoTrasPago = await ObtenerBalanceAsync(cliente);
        Assert.Equal(-150_000m, saldoAcumuladoTrasPago);
    }

    [Fact]
    public async Task PagoTotal_DejaSaldoPendienteEnCero_NoNegativo()
    {
        var (cliente, _) = await _factory.CrearClienteAutenticadoAsync();
        var categoria = await cliente.CrearCategoriaAsync("Restaurantes", TipoCategoria.Gasto);
        var tarjetaResp = await cliente.PostAsJsonAsync("/api/tarjetas-credito", new CrearTarjetaCreditoRequest("Mastercard Test", 5));
        var tarjeta = (await tarjetaResp.Content.ReadFromJsonAsync<TarjetaCreditoResponse>())!;

        await cliente.RegistrarMovimientoAsync(categoria.Id, 80_000m, tarjetaCreditoId: tarjeta.Id);
        await cliente.PostAsJsonAsync($"/api/tarjetas-credito/{tarjeta.Id}/pagos", new RegistrarPagoTarjetaRequest(80_000m));

        var saldo = await ObtenerSaldoPendienteAsync(cliente, tarjeta.Id);
        Assert.Equal(0m, saldo.Monto);
    }

    [Fact]
    public async Task UnIngreso_NoPuedePagarseConTarjeta()
    {
        var (cliente, _) = await _factory.CrearClienteAutenticadoAsync();
        var categoriaIngreso = await cliente.CrearCategoriaAsync("Salario", TipoCategoria.Ingreso);
        var tarjetaResp = await cliente.PostAsJsonAsync("/api/tarjetas-credito", new CrearTarjetaCreditoRequest("Visa Test", 20));
        var tarjeta = (await tarjetaResp.Content.ReadFromJsonAsync<TarjetaCreditoResponse>())!;

        var respuesta = await cliente.PostAsJsonAsync("/api/movimientos-diaadia",
            new CrearMovimientoDiaADiaRequest(categoriaIngreso.Id, 100_000m, null, null, tarjeta.Id));

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task EliminarTarjeta_NoBorraElGastoYaRegistrado_SoloPierdeLaEtiqueta()
    {
        var (cliente, _) = await _factory.CrearClienteAutenticadoAsync();
        var categoria = await cliente.CrearCategoriaAsync("Mercado", TipoCategoria.Gasto);
        var tarjetaResp = await cliente.PostAsJsonAsync("/api/tarjetas-credito", new CrearTarjetaCreditoRequest("Visa Test", 20));
        var tarjeta = (await tarjetaResp.Content.ReadFromJsonAsync<TarjetaCreditoResponse>())!;

        await cliente.RegistrarMovimientoAsync(categoria.Id, 60_000m, tarjetaCreditoId: tarjeta.Id);

        var eliminar = await cliente.DeleteAsync($"/api/tarjetas-credito/{tarjeta.Id}");
        eliminar.EnsureSuccessStatusCode();

        // El gasto y el saldo acumulado deben sobrevivir intactos: solo se
        // pierde la etiqueta de con qué tarjeta se pagó, no la plata gastada.
        var saldo = await ObtenerBalanceAsync(cliente);
        Assert.Equal(-60_000m, saldo);
    }

    private static async Task<decimal> ObtenerBalanceAsync(HttpClient cliente)
    {
        var resumen = await cliente.GetFromJsonAsync<ResumenDiaADiaResponse>("/api/movimientos-diaadia/resumen");
        return resumen!.Balance;
    }

    private static async Task<SaldoPendienteResponse> ObtenerSaldoPendienteAsync(HttpClient cliente, int tarjetaId)
    {
        return (await cliente.GetFromJsonAsync<SaldoPendienteResponse>($"/api/tarjetas-credito/{tarjetaId}/saldo-pendiente"))!;
    }
}
