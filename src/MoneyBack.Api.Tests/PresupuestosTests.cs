using System.Net.Http.Json;
using MoneyBack.Api.Dtos;
using MoneyBack.Api.Models.DiaADia;

namespace MoneyBack.Api.Tests;

public class PresupuestosTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public PresupuestosTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task MontoGastado_SoloSumaElMesYAnioDelPresupuesto_NoOtrosMeses()
    {
        var (cliente, _) = await _factory.CrearClienteAutenticadoAsync();
        var categoria = await cliente.CrearCategoriaAsync("Mercado", TipoCategoria.Gasto);

        var guardar = await cliente.PostAsJsonAsync("/api/presupuestos", new GuardarPresupuestoRequest(categoria.Id, 500_000m, 3, 2026));
        guardar.EnsureSuccessStatusCode();

        // Dentro del mes del presupuesto (marzo 2026).
        await cliente.RegistrarMovimientoAsync(categoria.Id, 100_000m, new DateTime(2026, 3, 5));
        await cliente.RegistrarMovimientoAsync(categoria.Id, 120_000m, new DateTime(2026, 3, 20));
        // Fuera del mes: no debe contar.
        await cliente.RegistrarMovimientoAsync(categoria.Id, 999_000m, new DateTime(2026, 4, 1));
        await cliente.RegistrarMovimientoAsync(categoria.Id, 999_000m, new DateTime(2026, 2, 28));

        var presupuestos = await cliente.GetFromJsonAsync<List<PresupuestoResponse>>("/api/presupuestos?mes=3&anio=2026");

        var presupuesto = Assert.Single(presupuestos!);
        Assert.Equal(220_000m, presupuesto.MontoGastado);
        Assert.Equal(500_000m, presupuesto.MontoLimite);
    }

    [Fact]
    public async Task GuardarPresupuestoDosVeces_MismoMesYCategoria_ReemplazaEnVezDeDuplicar()
    {
        var (cliente, _) = await _factory.CrearClienteAutenticadoAsync();
        var categoria = await cliente.CrearCategoriaAsync("Transporte", TipoCategoria.Gasto);

        await cliente.PostAsJsonAsync("/api/presupuestos", new GuardarPresupuestoRequest(categoria.Id, 200_000m, 6, 2026));
        await cliente.PostAsJsonAsync("/api/presupuestos", new GuardarPresupuestoRequest(categoria.Id, 350_000m, 6, 2026));

        var presupuestos = await cliente.GetFromJsonAsync<List<PresupuestoResponse>>("/api/presupuestos?mes=6&anio=2026");

        var presupuesto = Assert.Single(presupuestos!);
        Assert.Equal(350_000m, presupuesto.MontoLimite);
    }
}
