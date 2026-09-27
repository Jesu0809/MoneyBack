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
        )
    {
        var (clienteA, _) = await _factory.CrearClienteAutenticadoAsync();
        var (clienteB, usuarioB) = await _factory.CrearClienteAutenticadoAsync();

        var invitar = await clienteA.PostAsJsonAsync("/api/invitaciones-hogar",
            new CrearInvitacionHogarRequest(usuarioB.Email!));
        invitar.EnsureSuccessStatusCode();
        var invitacion = (await invitar.Content.ReadFromJsonAsync<InvitacionHogarResponse>())!;

        var aceptar = await clienteB.PostAsync($"/api/invitaciones-hogar/{invitacion.Id}/aceptar", null);
        aceptar.EnsureSuccessStatusCode();
        var hogar = (await aceptar.Content.ReadFromJsonAsync<HogarResponse>())!;

        var activar = await clienteA.PutAsJsonAsync("/api/hogares/mio",
            new ActualizarHogarRequest(false, true));
        activar.EnsureSuccessStatusCode();

        return (clienteA, hogar);
    }

    /// <summary>
    /// Con una sola meta da igual qué porcentaje tenga: el reparto se
    /// normaliza sobre lo que suman las metas activas, así que se lleva todo
    /// el vuelto. Si no fuera así, archivar una meta haría desaparecer parte
    /// del ahorro en el camino.
    /// </summary>
    [Fact]
    public async Task ConUnaSolaMeta_SeLlevaTodoElVueltoAunqueSuPorcentajeNoSea100()
    {
        var (cliente, hogar) = await CrearHogarConRedondeoAsync();

        var crearMeta = await cliente.PostAsJsonAsync($"/api/hogares/{hogar.Id}/metas",
            new CrearMetaRequest("Apto", 100_000_000m, "🏠", false, PorcentajeRedondeo: 30));
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
        var (cliente, hogar) = await CrearHogarConRedondeoAsync();
        var crearMeta = await cliente.PostAsJsonAsync($"/api/hogares/{hogar.Id}/metas",
            new CrearMetaRequest("Apto", 100_000_000m, "🏠", false, 80));
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
        var (cliente, hogar) = await CrearHogarConRedondeoAsync();

        var crearApto = await cliente.PostAsJsonAsync($"/api/hogares/{hogar.Id}/metas",
            new CrearMetaRequest("Apto", 100_000_000m, "🏠", false, PorcentajeRedondeo: 80));
        var apto = (await crearApto.Content.ReadFromJsonAsync<MetaResponse>())!;
        var crearEmergencia = await cliente.PostAsJsonAsync($"/api/hogares/{hogar.Id}/metas",
            new CrearMetaRequest("Emergencia", 10_000_000m, "🛟", true, PorcentajeRedondeo: 20));
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

    /// <summary>
    /// Una meta con 0% no participa del vuelto. Es lo que permite tener una
    /// meta de "Lavadora" a la que se aporta a mano sin que se lleve parte
    /// de lo que va al apartamento.
    /// </summary>
    [Fact]
    public async Task UnaMetaConCeroPorCientoNoRecibeVuelto()
    {
        var (cliente, hogar) = await CrearHogarConRedondeoAsync();

        var crearApto = await cliente.PostAsJsonAsync($"/api/hogares/{hogar.Id}/metas",
            new CrearMetaRequest("Apto", 100_000_000m, "🏠", false, PorcentajeRedondeo: 100));
        var apto = (await crearApto.Content.ReadFromJsonAsync<MetaResponse>())!;

        var crearLavadora = await cliente.PostAsJsonAsync($"/api/hogares/{hogar.Id}/metas",
            new CrearMetaRequest("Lavadora", 2_000_000m, "🧺", false, PorcentajeRedondeo: 0));
        var lavadora = (await crearLavadora.Content.ReadFromJsonAsync<MetaResponse>())!;

        var categoria = await cliente.CrearCategoriaAsync("Mercado", TipoCategoria.Gasto);
        await cliente.RegistrarMovimientoAsync(categoria.Id, 47_300m);

        Assert.Equal(700m, (await cliente.GetFromJsonAsync<MetaDetalleResponse>($"/api/metas/{apto.Id}"))!.MontoActual);
        Assert.Equal(0m, (await cliente.GetFromJsonAsync<MetaDetalleResponse>($"/api/metas/{lavadora.Id}"))!.MontoActual);
    }

    /// <summary>
    /// Tres metas, que es justo lo que el modelo viejo no permitía: los
    /// porcentajes vivían en el hogar y eran exactamente dos.
    /// </summary>
    [Fact]
    public async Task ConTresMetas_ElVueltoSeRepartePorSuPorcentaje()
    {
        var (cliente, hogar) = await CrearHogarConRedondeoAsync();

        async Task<MetaResponse> Crear(string nombre, decimal porcentaje)
        {
            var r = await cliente.PostAsJsonAsync($"/api/hogares/{hogar.Id}/metas",
                new CrearMetaRequest(nombre, 10_000_000m, "🎯", false, porcentaje));
            return (await r.Content.ReadFromJsonAsync<MetaResponse>())!;
        }

        var apto = await Crear("Apto", 50);
        var emergencia = await Crear("Emergencia", 30);
        var lavadora = await Crear("Lavadora", 20);

        var categoria = await cliente.CrearCategoriaAsync("Mercado", TipoCategoria.Gasto);
        // 48.000 - 47.300 = 700 de vuelto: 350 + 210 + 140.
        await cliente.RegistrarMovimientoAsync(categoria.Id, 47_300m);

        Assert.Equal(350m, (await cliente.GetFromJsonAsync<MetaDetalleResponse>($"/api/metas/{apto.Id}"))!.MontoActual);
        Assert.Equal(210m, (await cliente.GetFromJsonAsync<MetaDetalleResponse>($"/api/metas/{emergencia.Id}"))!.MontoActual);
        Assert.Equal(140m, (await cliente.GetFromJsonAsync<MetaDetalleResponse>($"/api/metas/{lavadora.Id}"))!.MontoActual);
    }
}
