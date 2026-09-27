using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using MoneyBack.Api.Dtos;
using MoneyBack.Api.Models.Metas;

namespace MoneyBack.Api.Tests;

/// <summary>
/// Entrar a un grupo con la plata de otras personas no puede pasar sin que
/// esa persona diga que sí. Antes bastaba con que alguien escribiera tu
/// correo y quedabas vinculado.
///
/// Y ahora se invita A UN GRUPO que ya existe, no "armemos algo entre los
/// dos": es lo que permite que un grupo tenga tres personas, o que alguien
/// empiece a ahorrar solo y sume gente después.
/// </summary>
public class InvitacionesHogarTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public InvitacionesHogarTests(ApiFactory factory) => _factory = factory;

    private async Task<(HttpClient Cliente, HogarResponse Hogar)> CrearGrupoAsync(string nombre = "Nosotros")
    {
        var (cliente, _) = await _factory.CrearClienteAutenticadoAsync();
        var creado = await cliente.PostAsJsonAsync("/api/hogares", new CrearHogarRequest(nombre));
        creado.EnsureSuccessStatusCode();
        return (cliente, (await creado.Content.ReadFromJsonAsync<HogarResponse>())!);
    }

    private static Task<HttpResponseMessage> InvitarAsync(HttpClient cliente, string email, int hogarId) =>
        cliente.PostAsJsonAsync("/api/invitaciones-hogar", new CrearInvitacionHogarRequest(email, hogarId));

    [Fact]
    public async Task InvitarNoMeteANadieAlGrupoTodavia()
    {
        var (clienteA, hogar) = await CrearGrupoAsync();
        var (_, usuarioB) = await _factory.CrearClienteAutenticadoAsync();

        var respuesta = await InvitarAsync(clienteA, usuarioB.Email!, hogar.Id);
        respuesta.EnsureSuccessStatusCode();

        var actual = await clienteA.GetFromJsonAsync<HogarResponse>($"/api/hogares/{hogar.Id}");
        Assert.Single(actual!.Miembros);
    }

    [Fact]
    public async Task AlAceptar_LaPersonaQuedaDentroYLoVenLosDos()
    {
        var (clienteA, clienteB, hogar) = await _factory.CrearGrupoDeDosAsync();

        Assert.Equal(2, hogar.Miembros.Count);

        var desdeB = await clienteB.GetFromJsonAsync<HogarResponse>($"/api/hogares/{hogar.Id}");
        Assert.Equal(2, desdeB!.Miembros.Count);

        // Quien lo creó queda de administrador; quien llega después, no.
        Assert.True((await clienteA.GetFromJsonAsync<HogarResponse>($"/api/hogares/{hogar.Id}"))!.SoyAdministrador);
        Assert.False(desdeB.SoyAdministrador);
    }

    /// <summary>
    /// Lo que el modelo viejo hacía imposible: un grupo era exactamente dos
    /// columnas, así que no cabía una tercera persona.
    /// </summary>
    [Fact]
    public async Task UnGrupoPuedeTenerTresOMasPersonas()
    {
        var (clienteA, _, hogar) = await _factory.CrearGrupoDeDosAsync("Los Nuncira");
        var (clienteC, usuarioC) = await _factory.CrearClienteAutenticadoAsync();

        var invitar = await InvitarAsync(clienteA, usuarioC.Email!, hogar.Id);
        var invitacion = (await invitar.Content.ReadFromJsonAsync<InvitacionHogarResponse>())!;
        (await clienteC.PostAsync($"/api/invitaciones-hogar/{invitacion.Id}/aceptar", null)).EnsureSuccessStatusCode();

        var actual = await clienteA.GetFromJsonAsync<HogarResponse>($"/api/hogares/{hogar.Id}");
        Assert.Equal(3, actual!.Miembros.Count);
    }

    /// <summary>
    /// Y lo otro que no se podía: estar en varios grupos a la vez — el de la
    /// pareja y el de la familia.
    /// </summary>
    [Fact]
    public async Task UnaPersonaPuedeEstarEnVariosGrupos()
    {
        var (cliente, _) = await _factory.CrearClienteAutenticadoAsync();

        foreach (var nombre in new[] { "Con Juliana", "Los Nuncira", "Viaje" })
        {
            (await cliente.PostAsJsonAsync("/api/hogares", new CrearHogarRequest(nombre))).EnsureSuccessStatusCode();
        }

        var mios = await cliente.GetFromJsonAsync<List<HogarResponse>>("/api/hogares");
        Assert.Equal(3, mios!.Count);
        Assert.Contains(mios, g => g.Nombre == "Con Juliana");
    }

    [Fact]
    public async Task RechazarNoMeteANadie()
    {
        var (clienteA, hogar) = await CrearGrupoAsync();
        var (clienteB, usuarioB) = await _factory.CrearClienteAutenticadoAsync();

        var invitar = await InvitarAsync(clienteA, usuarioB.Email!, hogar.Id);
        var invitacion = (await invitar.Content.ReadFromJsonAsync<InvitacionHogarResponse>())!;

        (await clienteB.PostAsync($"/api/invitaciones-hogar/{invitacion.Id}/rechazar", null)).EnsureSuccessStatusCode();

        var actual = await clienteA.GetFromJsonAsync<HogarResponse>($"/api/hogares/{hogar.Id}");
        Assert.Single(actual!.Miembros);
    }

    [Fact]
    public async Task SoloLaPersonaInvitadaPuedeAceptar()
    {
        var (clienteA, hogar) = await CrearGrupoAsync();
        var (_, usuarioB) = await _factory.CrearClienteAutenticadoAsync();

        var invitar = await InvitarAsync(clienteA, usuarioB.Email!, hogar.Id);
        var invitacion = (await invitar.Content.ReadFromJsonAsync<InvitacionHogarResponse>())!;

        var intento = await clienteA.PostAsync($"/api/invitaciones-hogar/{invitacion.Id}/aceptar", null);
        Assert.Equal(HttpStatusCode.Forbidden, intento.StatusCode);
    }

    [Fact]
    public async Task NoSeInvitaDosVecesALaMismaPersonaAlMismoGrupo()
    {
        var (clienteA, hogar) = await CrearGrupoAsync();
        var (_, usuarioB) = await _factory.CrearClienteAutenticadoAsync();

        (await InvitarAsync(clienteA, usuarioB.Email!, hogar.Id)).EnsureSuccessStatusCode();
        var segunda = await InvitarAsync(clienteA, usuarioB.Email!, hogar.Id);

        Assert.Equal(HttpStatusCode.Conflict, segunda.StatusCode);
    }

    [Fact]
    public async Task NoSeInvitaAQuienYaEstaEnElGrupo()
    {
        var (clienteA, clienteB, hogar) = await _factory.CrearGrupoDeDosAsync();
        var emailB = (await clienteB.GetFromJsonAsync<PerfilResponse>("/api/auth/me"))!.Email;

        var respuesta = await InvitarAsync(clienteA, emailB, hogar.Id);
        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
    }

    [Fact]
    public async Task NoSePuedeInvitarAUnGrupoAjeno()
    {
        var (_, hogar) = await CrearGrupoAsync();
        var (extrano, _) = await _factory.CrearClienteAutenticadoAsync();
        var (_, usuarioC) = await _factory.CrearClienteAutenticadoAsync();

        var respuesta = await InvitarAsync(extrano, usuarioC.Email!, hogar.Id);
        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    /// <summary>
    /// Quedarse sin nadie dejaría el ahorro sin dueño: nadie podría verlo ni
    /// sacarlo.
    /// </summary>
    [Fact]
    public async Task LaUnicaPersonaDeUnGrupoNoSePuedeSalir()
    {
        var (cliente, hogar) = await CrearGrupoAsync();

        var salir = await cliente.PostAsync($"/api/hogares/{hogar.Id}/salir", null);
        Assert.Equal(HttpStatusCode.Conflict, salir.StatusCode);
    }

    /// <summary>
    /// Si se va quien administraba, alguien tiene que quedar: un grupo sin
    /// administrador no se puede renombrar ni invitar a nadie más.
    /// </summary>
    [Fact]
    public async Task SiSeVaQuienAdministra_OtroTomaElRelevo()
    {
        var (clienteA, clienteB, hogar) = await _factory.CrearGrupoDeDosAsync();

        (await clienteA.PostAsync($"/api/hogares/{hogar.Id}/salir", null)).EnsureSuccessStatusCode();

        var desdeB = await clienteB.GetFromJsonAsync<HogarResponse>($"/api/hogares/{hogar.Id}");
        Assert.Single(desdeB!.Miembros);
        Assert.True(desdeB.SoyAdministrador);
    }

    [Fact]
    public async Task LaPersonaInvitadaVeLaInvitacionPendiente()
    {
        var (clienteA, hogar) = await CrearGrupoAsync();
        var (clienteB, usuarioB) = await _factory.CrearClienteAutenticadoAsync();

        await InvitarAsync(clienteA, usuarioB.Email!, hogar.Id);

        var mias = await clienteB.GetFromJsonAsync<MisInvitacionesHogarResponse>("/api/invitaciones-hogar/mia");
        Assert.NotNull(mias!.Recibida);
        Assert.Equal(EstadoInvitacionHogar.Pendiente, mias.Recibida!.Estado);
    }
}
