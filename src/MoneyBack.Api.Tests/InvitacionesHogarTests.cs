using System.Net;
using System.Net.Http.Json;
using MoneyBack.Api.Dtos;

namespace MoneyBack.Api.Tests;

/// <summary>
/// Antes, escribir el correo de tu pareja vinculaba el hogar de una, sin que
/// ella confirmara nada. Estos tests protegen que ahora el Hogar solo nazca
/// cuando el invitado de verdad acepta.
/// </summary>
public class InvitacionesHogarTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public InvitacionesHogarTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task CrearInvitacion_NoCreaHogarTodavia()
    {
        var (clienteA, _) = await _factory.CrearClienteAutenticadoAsync();
        var (clienteB, usuarioB) = await _factory.CrearClienteAutenticadoAsync();

        var respuesta = await clienteA.PostAsJsonAsync("/api/invitaciones-hogar", new CrearInvitacionHogarRequest(usuarioB.Email!));
        respuesta.EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.NotFound, (await clienteA.GetAsync("/api/hogares/mio")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await clienteB.GetAsync("/api/hogares/mio")).StatusCode);
    }

    [Fact]
    public async Task Aceptar_CreaElHogarYQuedaVisibleParaAmbos()
    {
        var (clienteA, _) = await _factory.CrearClienteAutenticadoAsync();
        var (clienteB, usuarioB) = await _factory.CrearClienteAutenticadoAsync();

        var invitar = await clienteA.PostAsJsonAsync("/api/invitaciones-hogar", new CrearInvitacionHogarRequest(usuarioB.Email!));
        var invitacion = (await invitar.Content.ReadFromJsonAsync<InvitacionHogarResponse>())!;

        var aceptar = await clienteB.PostAsync($"/api/invitaciones-hogar/{invitacion.Id}/aceptar", null);
        aceptar.EnsureSuccessStatusCode();

        Assert.True((await clienteA.GetAsync("/api/hogares/mio")).IsSuccessStatusCode);
        Assert.True((await clienteB.GetAsync("/api/hogares/mio")).IsSuccessStatusCode);
    }

    [Fact]
    public async Task Rechazar_NoCreaHogar()
    {
        var (clienteA, _) = await _factory.CrearClienteAutenticadoAsync();
        var (clienteB, usuarioB) = await _factory.CrearClienteAutenticadoAsync();

        var invitar = await clienteA.PostAsJsonAsync("/api/invitaciones-hogar", new CrearInvitacionHogarRequest(usuarioB.Email!));
        var invitacion = (await invitar.Content.ReadFromJsonAsync<InvitacionHogarResponse>())!;

        var rechazar = await clienteB.PostAsync($"/api/invitaciones-hogar/{invitacion.Id}/rechazar", null);
        rechazar.EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.NotFound, (await clienteA.GetAsync("/api/hogares/mio")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await clienteB.GetAsync("/api/hogares/mio")).StatusCode);
    }

    [Fact]
    public async Task SoloElInvitado_PuedeAceptar_NoQuienInvito()
    {
        var (clienteA, _) = await _factory.CrearClienteAutenticadoAsync();
        var (_, usuarioB) = await _factory.CrearClienteAutenticadoAsync();

        var invitar = await clienteA.PostAsJsonAsync("/api/invitaciones-hogar", new CrearInvitacionHogarRequest(usuarioB.Email!));
        var invitacion = (await invitar.Content.ReadFromJsonAsync<InvitacionHogarResponse>())!;

        var intentoDelInvitador = await clienteA.PostAsync($"/api/invitaciones-hogar/{invitacion.Id}/aceptar", null);

        Assert.Equal(HttpStatusCode.Forbidden, intentoDelInvitador.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await clienteA.GetAsync("/api/hogares/mio")).StatusCode);
    }

    [Fact]
    public async Task NoSePuedeInvitarDeNuevo_MientrasHayaUnaInvitacionPendiente()
    {
        var (clienteA, _) = await _factory.CrearClienteAutenticadoAsync();
        var (_, usuarioB) = await _factory.CrearClienteAutenticadoAsync();

        var primera = await clienteA.PostAsJsonAsync("/api/invitaciones-hogar", new CrearInvitacionHogarRequest(usuarioB.Email!));
        primera.EnsureSuccessStatusCode();

        var segunda = await clienteA.PostAsJsonAsync("/api/invitaciones-hogar", new CrearInvitacionHogarRequest(usuarioB.Email!));

        Assert.Equal(HttpStatusCode.Conflict, segunda.StatusCode);
    }

    [Fact]
    public async Task ElInvitado_VeLaInvitacionPendienteEnMia()
    {
        var (clienteA, usuarioA) = await _factory.CrearClienteAutenticadoAsync();
        var (clienteB, usuarioB) = await _factory.CrearClienteAutenticadoAsync();

        await clienteA.PostAsJsonAsync("/api/invitaciones-hogar", new CrearInvitacionHogarRequest(usuarioB.Email!));

        var misInvitacionesB = await clienteB.GetFromJsonAsync<MisInvitacionesHogarResponse>("/api/invitaciones-hogar/mia");
        var misInvitacionesA = await clienteA.GetFromJsonAsync<MisInvitacionesHogarResponse>("/api/invitaciones-hogar/mia");

        Assert.NotNull(misInvitacionesB!.Recibida);
        Assert.Equal(usuarioA.Id, misInvitacionesB.Recibida!.InvitadorId);
        Assert.NotNull(misInvitacionesA!.Enviada);
        Assert.Null(misInvitacionesA.Recibida);
    }
}
