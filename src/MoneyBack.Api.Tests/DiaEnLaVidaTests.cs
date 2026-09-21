using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using MoneyBack.Api.Data;
using MoneyBack.Api.Dtos;
using MoneyBack.Api.Models.DiaADia;
using MoneyBack.Api.Models.Metas;
using MoneyBack.Api.Services;

namespace MoneyBack.Api.Tests;

/// <summary>
/// Simula un mes normal de uso de una persona real: sueldo, arriendo,
/// mercado varias veces (algunos montos "feos" que sí redondean), una compra
/// con tarjeta, un abono parcial a la tarjeta, y un presupuesto que sí se
/// pasa. No prueba una sola pieza aislada — prueba que todas encajen entre
/// sí al mismo tiempo, que es justo donde un bug de un componente puede
/// esconderse detrás de que los demás sí den bien.
/// </summary>
public class DiaEnLaVidaTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public DiaEnLaVidaTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task UnMesNormalDeGastosEIngresos_CuadraEnTodasLasVistasALaVez()
    {
        var (clienteA, usuarioA) = await _factory.CrearClienteAutenticadoAsync();
        var (clienteB, usuarioB) = await _factory.CrearClienteAutenticadoAsync();

        // Hogar con redondeo 20% Emergencia / 80% Apartamento — la pareja
        // debe aceptar la invitación, ya no se vincula solo con el correo.
        var invitacion = await clienteA.PostAsJsonAsync("/api/invitaciones-hogar", new CrearInvitacionHogarRequest(usuarioB.Email!, false, 20, 80));
        var invitacionCreada = (await invitacion.Content.ReadFromJsonAsync<InvitacionHogarResponse>())!;
        await clienteB.PostAsync($"/api/invitaciones-hogar/{invitacionCreada.Id}/aceptar", null);
        await clienteA.PutAsJsonAsync("/api/hogares/mio", new ActualizarHogarRequest(false, true, 20, 80));
        var hogar = await clienteA.GetFromJsonAsync<HogarResponse>("/api/hogares/mio");

        var crearApto = await clienteA.PostAsJsonAsync($"/api/hogares/{hogar!.Id}/metas",
            new CrearMetaRequest(TipoMeta.Apartamento, "Apto", 200_000_000m));
        var apto = (await crearApto.Content.ReadFromJsonAsync<MetaResponse>())!;
        var crearEmergencia = await clienteA.PostAsJsonAsync($"/api/hogares/{hogar.Id}/metas",
            new CrearMetaRequest(TipoMeta.Emergencia, "Emergencia", 10_000_000m));
        var emergencia = (await crearEmergencia.Content.ReadFromJsonAsync<MetaResponse>())!;

        var salario = await clienteA.CrearCategoriaAsync("Salario", TipoCategoria.Ingreso);
        var mercado = await clienteA.CrearCategoriaAsync("Mercado", TipoCategoria.Gasto);
        var arriendo = await clienteA.CrearCategoriaAsync("Arriendo", TipoCategoria.Gasto);
        var transporte = await clienteA.CrearCategoriaAsync("Transporte", TipoCategoria.Gasto);
        var restaurantes = await clienteA.CrearCategoriaAsync("Restaurantes", TipoCategoria.Gasto);

        var tarjetaResp = await clienteA.PostAsJsonAsync("/api/tarjetas-credito", new CrearTarjetaCreditoRequest("Visa", 20));
        var tarjeta = (await tarjetaResp.Content.ReadFromJsonAsync<TarjetaCreditoResponse>())!;

        await clienteA.PostAsJsonAsync("/api/presupuestos", new GuardarPresupuestoRequest(mercado.Id, 300_000m, 6, 2026));

        // El mes, como lo viviría una persona: no en orden perfecto, montos
        // "feos" (no múltiplos de mil) donde de verdad se pagaría así.
        await clienteA.RegistrarMovimientoAsync(salario.Id, 3_000_000m, new DateTime(2026, 6, 1));
        await clienteA.RegistrarMovimientoAsync(arriendo.Id, 1_200_000m, new DateTime(2026, 6, 1));
        await clienteA.RegistrarMovimientoAsync(mercado.Id, 187_500m, new DateTime(2026, 6, 3));       // vuelto 500
        await clienteA.RegistrarMovimientoAsync(transporte.Id, 45_000m, new DateTime(2026, 6, 5));      // exacto, sin vuelto
        await clienteA.RegistrarMovimientoAsync(restaurantes.Id, 92_300m, new DateTime(2026, 6, 6), tarjetaCreditoId: tarjeta.Id); // vuelto 700
        await clienteA.RegistrarMovimientoAsync(mercado.Id, 63_200m, new DateTime(2026, 6, 15));        // vuelto 800
        var pago = await clienteA.PostAsJsonAsync($"/api/tarjetas-credito/{tarjeta.Id}/pagos", new RegistrarPagoTarjetaRequest(50_000m));
        pago.EnsureSuccessStatusCode();

        // --- Saldo acumulado ---
        var resumen = await clienteA.GetFromJsonAsync<ResumenDiaADiaResponse>("/api/movimientos-diaadia/resumen");
        Assert.Equal(3_000_000m, resumen!.TotalIngresos);
        Assert.Equal(1_588_000m, resumen.TotalGastos); // 1.200.000 + 187.500 + 45.000 + 92.300 + 63.200
        Assert.Equal(1_412_000m, resumen.Balance);

        // --- Tarjeta: la compra ya está en TotalGastos de arriba; el pago
        // parcial NO debe restar de ahí otra vez, solo bajar lo pendiente. ---
        var saldoPendiente = await clienteA.GetFromJsonAsync<SaldoPendienteResponse>($"/api/tarjetas-credito/{tarjeta.Id}/saldo-pendiente");
        Assert.Equal(42_300m, saldoPendiente!.Monto); // 92.300 - 50.000

        // --- Presupuesto de Mercado: solo cuenta lo de Mercado, no el mes entero. ---
        var presupuestos = await clienteA.GetFromJsonAsync<List<PresupuestoResponse>>("/api/presupuestos?mes=6&anio=2026");
        var presupuestoMercado = Assert.Single(presupuestos!);
        Assert.Equal(250_700m, presupuestoMercado.MontoGastado); // 187.500 + 63.200

        // --- Redondeo: 500+700+800 = 2.000 repartidos 80/20 entre las metas. ---
        var detalleApto = await clienteA.GetFromJsonAsync<MetaDetalleResponse>($"/api/metas/{apto.Id}");
        var detalleEmergencia = await clienteA.GetFromJsonAsync<MetaDetalleResponse>($"/api/metas/{emergencia.Id}");
        Assert.Equal(1_600m, detalleApto!.MontoActual);
        Assert.Equal(400m, detalleEmergencia!.MontoActual);

        // --- Insight: a día 20 del mes, el ritmo de Mercado ya proyecta
        // pasarse del presupuesto de 300.000 (250.700 / 20 días * 30 días
        // ≈ 376.050) — es exactamente la clase de aviso que se manda el
        // domingo en la noche. ---
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tip = await InsightsService.CalcularTipAsync(usuarioA.Id, db, new DateTime(2026, 6, 20));

        Assert.NotNull(tip);
        Assert.Contains("Mercado", tip);
    }
}
