using System.Net;
using System.Net.Http.Json;
using MoneyBack.Api.Dtos;

namespace MoneyBack.Api.Tests;

/// <summary>
/// La app es por invitación, y hasta ahora la única llave era un código
/// global: compartirlo con alguien era darle la misma llave que a todos, sin
/// saber quién entró por quién ni poder revocarle el acceso a uno solo. Estas
/// pruebas cubren lo que hace que un enlace personal sea distinto de eso —
/// que sirva una vez, que venza, y que se sepa a quién dejó entrar.
/// </summary>
public class InvitacionesAppTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public InvitacionesAppTests(ApiFactory factory) => _factory = factory;

    private static async Task<string> CrearInvitacionAsync(HttpClient cliente)
    {
        var respuesta = await cliente.PostAsync("/api/invitaciones-app", null);
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<InvitacionAppCreadaResponse>())!.Token;
    }

    private async Task<HttpResponseMessage> RegistrarseAsync(string codigo, string? email = null)
    {
        var cliente = _factory.CreateClient();
        return await cliente.PostAsJsonAsync("/api/auth/register", new RegistrarUsuarioRequest(
            "Invitado", email ?? $"{Guid.NewGuid():N}@test.moneyback", "ClaveSegura#2026", codigo));
    }

    [Fact]
    public async Task ConElEnlaceDeAlguien_SePuedeEntrarSinElCodigoGeneral()
    {
        var (cliente, _) = await _factory.CrearClienteAutenticadoAsync();
        var token = await CrearInvitacionAsync(cliente);

        var respuesta = await RegistrarseAsync(token);

        Assert.True(respuesta.IsSuccessStatusCode, await respuesta.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Si sirviera dos veces, el primero que reenvíe el enlace por un chat
    /// abre la puerta a cualquiera — que es exactamente el problema del
    /// código global que esto vino a resolver.
    /// </summary>
    [Fact]
    public async Task ElMismoEnlaceNoSirveDosVeces()
    {
        var (cliente, _) = await _factory.CrearClienteAutenticadoAsync();
        var token = await CrearInvitacionAsync(cliente);

        (await RegistrarseAsync(token)).EnsureSuccessStatusCode();

        var segunda = await RegistrarseAsync(token);
        Assert.Equal(HttpStatusCode.BadRequest, segunda.StatusCode);
    }

    /// <summary>
    /// Si el registro falla, la invitación no se gastó: obligar a pedir un
    /// enlace nuevo porque la contraseña era corta sería absurdo.
    /// </summary>
    [Fact]
    public async Task SiElRegistroFalla_ElEnlaceSigueSirviendo()
    {
        var (cliente, _) = await _factory.CrearClienteAutenticadoAsync();
        var token = await CrearInvitacionAsync(cliente);

        var sinClave = _factory.CreateClient();
        var fallida = await sinClave.PostAsJsonAsync("/api/auth/register",
            new RegistrarUsuarioRequest("Invitado", "corta@test.moneyback", "abc", token));
        Assert.False(fallida.IsSuccessStatusCode);

        var reintento = await RegistrarseAsync(token);
        Assert.True(reintento.IsSuccessStatusCode);
    }

    [Fact]
    public async Task QuienInvitaVeSiSuEnlaceYaSeUsoYPorQuien()
    {
        var (cliente, _) = await _factory.CrearClienteAutenticadoAsync();
        var token = await CrearInvitacionAsync(cliente);

        var antes = (await cliente.GetFromJsonAsync<List<InvitacionAppResponse>>("/api/invitaciones-app"))!;
        Assert.Equal("Pendiente", Assert.Single(antes).Estado);

        (await RegistrarseAsync(token)).EnsureSuccessStatusCode();

        var despues = Assert.Single((await cliente.GetFromJsonAsync<List<InvitacionAppResponse>>("/api/invitaciones-app"))!);
        Assert.Equal("Usada", despues.Estado);
        Assert.Equal("Invitado", despues.UsadaPorNombre);
    }

    [Fact]
    public async Task AnularUnEnlaceLoDejaSinEfecto()
    {
        var (cliente, _) = await _factory.CrearClienteAutenticadoAsync();
        var token = await CrearInvitacionAsync(cliente);
        var invitacion = Assert.Single((await cliente.GetFromJsonAsync<List<InvitacionAppResponse>>("/api/invitaciones-app"))!);

        (await cliente.DeleteAsync($"/api/invitaciones-app/{invitacion.Id}")).EnsureSuccessStatusCode();

        var intento = await RegistrarseAsync(token);
        Assert.Equal(HttpStatusCode.BadRequest, intento.StatusCode);
    }

    /// <summary>
    /// Anular una ya usada no sacaría a nadie de la app, pero sí borraría el
    /// rastro de quién invitó a quién.
    /// </summary>
    [Fact]
    public async Task NoSePuedeAnularUnaInvitacionYaUsada()
    {
        var (cliente, _) = await _factory.CrearClienteAutenticadoAsync();
        var token = await CrearInvitacionAsync(cliente);
        (await RegistrarseAsync(token)).EnsureSuccessStatusCode();

        var invitacion = Assert.Single((await cliente.GetFromJsonAsync<List<InvitacionAppResponse>>("/api/invitaciones-app"))!);
        var intento = await cliente.DeleteAsync($"/api/invitaciones-app/{invitacion.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, intento.StatusCode);
    }

    [Fact]
    public async Task NadieVeNiAnulaLasInvitacionesDeOtro()
    {
        var (clienteA, _) = await _factory.CrearClienteAutenticadoAsync();
        var (clienteB, _) = await _factory.CrearClienteAutenticadoAsync();

        await CrearInvitacionAsync(clienteA);
        var deA = Assert.Single((await clienteA.GetFromJsonAsync<List<InvitacionAppResponse>>("/api/invitaciones-app"))!);

        Assert.Empty((await clienteB.GetFromJsonAsync<List<InvitacionAppResponse>>("/api/invitaciones-app"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await clienteB.DeleteAsync($"/api/invitaciones-app/{deA.Id}")).StatusCode);
    }

    [Fact]
    public async Task NoSePuedenAcumularEnlacesVivosSinLimite()
    {
        var (cliente, _) = await _factory.CrearClienteAutenticadoAsync();

        for (var i = 0; i < 5; i++) await CrearInvitacionAsync(cliente);

        var sexta = await cliente.PostAsync("/api/invitaciones-app", null);
        Assert.Equal(HttpStatusCode.BadRequest, sexta.StatusCode);

        // Anular una libera el cupo: el límite es sobre enlaces vivos, no un
        // castigo permanente.
        var alguna = (await cliente.GetFromJsonAsync<List<InvitacionAppResponse>>("/api/invitaciones-app"))!.First();
        await cliente.DeleteAsync($"/api/invitaciones-app/{alguna.Id}");

        (await cliente.PostAsync("/api/invitaciones-app", null)).EnsureSuccessStatusCode();
    }
}
