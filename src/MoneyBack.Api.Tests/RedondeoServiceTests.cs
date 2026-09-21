using System.Net.Http.Json;
using MoneyBack.Api.Dtos;
using MoneyBack.Api.Models.DiaADia;
using MoneyBack.Api.Models.Metas;

namespace MoneyBack.Api.Tests;

/// <summary>
/// El redondeo automático es dinero real moviéndose solo, sin que el usuario
/// lo pida explícitamente cada vez — por eso es tan importante que la
/// aritmética y el reparto por porcentajes sean exactos.
/// </summary>
public class RedondeoServiceTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public RedondeoServiceTests(ApiFactory factory) => _factory = factory;

    private async Task<(HttpClient Cliente, HogarResponse Hogar)> CrearHogarConRedondeoAsync(
        decimal porcentajeEmergencia, decimal porcentajeApartamento)
    {
        var (clienteA, _) = await _factory.CrearClienteAutenticadoAsync();
        var (clienteB, usuarioB) = await _factory.CrearClienteAutenticadoAsync();

        var invitar = await clienteA.PostAsJsonAsync("/api/invitaciones-hogar",
            new CrearInvitacionHogarRequest(usuarioB.Email!, false, porcentajeEmergencia, porcentajeApartamento));
        invitar.EnsureSuccessStatusCode();
        var invitacion = (await invitar.Content.ReadFromJsonAsync<InvitacionHogarResponse>())!;

        var aceptar = await clienteB.PostAsync($"/api/invitaciones-hogar/{invitacion.Id}/aceptar", null);
        aceptar.EnsureSuccessStatusCode();
        var hogar = (await aceptar.Content.ReadFromJsonAsync<HogarResponse>())!;

        var activar = await clienteA.PutAsJsonAsync("/api/hogares/mio",
            new ActualizarHogarRequest(false, true, porcentajeEmergencia, porcentajeApartamento));
        activar.EnsureSuccessStatusCode();

        return (clienteA, hogar);
    }

    [Fact]
    public async Task GastoNoMultiploDeMil_AportaElVuelto100PorCientoAUnaSolaMeta()
    {
        var (cliente, hogar) = await CrearHogarConRedondeoAsync(porcentajeEmergencia: 0, porcentajeApartamento: 100);

        var crearMeta = await cliente.PostAsJsonAsync($"/api/hogares/{hogar.Id}/metas",
            new CrearMetaRequest(TipoMeta.Apartamento, "Apto", 100_000_000m));
        var meta = (await crearMeta.Content.ReadFromJsonAsync<MetaResponse>())!;

        var categoria = await cliente.CrearCategoriaAsync("Mercado", TipoCategoria.Gasto);
        // 47.300 -> redondea hacia arriba a 48.000 -> vuelto de 700.
        var movimiento = await cliente.RegistrarMovimientoAsync(categoria.Id, 47_300m);
        Assert.True(movimiento.RedondeoAplicado);

        var detalle = await cliente.GetFromJsonAsync<MetaDetalleResponse>($"/api/metas/{meta.Id}");
        Assert.Equal(700m, detalle!.MontoActual);
    }

    [Fact]
    public async Task GastoExactoMultiploDeMil_NoGeneraAporte()
    {
        var (cliente, hogar) = await CrearHogarConRedondeoAsync(porcentajeEmergencia: 0, porcentajeApartamento: 100);
        var crearMeta = await cliente.PostAsJsonAsync($"/api/hogares/{hogar.Id}/metas",
            new CrearMetaRequest(TipoMeta.Apartamento, "Apto", 100_000_000m));
        var meta = (await crearMeta.Content.ReadFromJsonAsync<MetaResponse>())!;

        var categoria = await cliente.CrearCategoriaAsync("Mercado", TipoCategoria.Gasto);
        var movimiento = await cliente.RegistrarMovimientoAsync(categoria.Id, 50_000m);

        Assert.False(movimiento.RedondeoAplicado);
        var detalle = await cliente.GetFromJsonAsync<MetaDetalleResponse>($"/api/metas/{meta.Id}");
        Assert.Equal(0m, detalle!.MontoActual);
    }

    [Fact]
    public async Task ConDosMetasActivas_ElVueltoSeRepartePorPorcentaje()
    {
        var (cliente, hogar) = await CrearHogarConRedondeoAsync(porcentajeEmergencia: 20, porcentajeApartamento: 80);

        var crearApto = await cliente.PostAsJsonAsync($"/api/hogares/{hogar.Id}/metas",
            new CrearMetaRequest(TipoMeta.Apartamento, "Apto", 100_000_000m));
        var apto = (await crearApto.Content.ReadFromJsonAsync<MetaResponse>())!;
        var crearEmergencia = await cliente.PostAsJsonAsync($"/api/hogares/{hogar.Id}/metas",
            new CrearMetaRequest(TipoMeta.Emergencia, "Emergencia", 10_000_000m));
        var emergencia = (await crearEmergencia.Content.ReadFromJsonAsync<MetaResponse>())!;

        var categoria = await cliente.CrearCategoriaAsync("Mercado", TipoCategoria.Gasto);
        // 48.300 -> redondea a 49.000 -> vuelto de 700: 560 a Apartamento (80%), 140 a Emergencia (20%).
        await cliente.RegistrarMovimientoAsync(categoria.Id, 48_300m);

        var detalleApto = await cliente.GetFromJsonAsync<MetaDetalleResponse>($"/api/metas/{apto.Id}");
        var detalleEmergencia = await cliente.GetFromJsonAsync<MetaDetalleResponse>($"/api/metas/{emergencia.Id}");

        Assert.Equal(560m, detalleApto!.MontoActual);
        Assert.Equal(140m, detalleEmergencia!.MontoActual);
    }

    [Fact]
    public async Task SinHogar_ElGastoSeRegistraNormalYSinRedondeo()
    {
        var (cliente, _) = await _factory.CrearClienteAutenticadoAsync();
        var categoria = await cliente.CrearCategoriaAsync("Mercado", TipoCategoria.Gasto);

        var movimiento = await cliente.RegistrarMovimientoAsync(categoria.Id, 47_300m);

        Assert.False(movimiento.RedondeoAplicado);
    }
}
