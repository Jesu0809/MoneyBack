using System.Net;
using System.Net.Http.Json;
using MoneyBack.Api.Dtos;

namespace MoneyBack.Api.Tests;

/// <summary>
/// Que el administrador pueda reajustar la contraseña de una cuenta es,
/// literalmente, la llave de la casa de otra persona. Por eso se prueba no
/// solo que funcione, sino que haga todo lo que tiene que hacer alrededor:
/// invalidar lo viejo y no dejar puertas abiertas.
/// </summary>
public class ClaveTemporalAdminTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ClaveTemporalAdminTests(ApiFactory factory) => _factory = factory;

    private async Task<(HttpClient Admin, int UsuarioId, string Email)> PrepararAsync()
    {
        var (admin, _) = await _factory.CrearAdminAutenticadoAsync();
        var email = $"{Guid.NewGuid():N}@test.moneyback";
        var (_, usuario) = await _factory.CrearClienteAutenticadoAsync(email);
        return (admin, usuario.Id, email);
    }

    [Fact]
    public async Task LaClaveTemporalSirveParaEntrar()
    {
        var (admin, usuarioId, email) = await PrepararAsync();

        var respuesta = await admin.PostAsync($"/api/admin/usuarios/{usuarioId}/clave-temporal", null);
        respuesta.EnsureSuccessStatusCode();
        var clave = (await respuesta.Content.ReadFromJsonAsync<ClaveTemporalResponse>())!.Clave;

        var login = await _factory.CreateClient()
            .PostAsJsonAsync("/api/auth/login", new LoginRequest(email, clave));

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task LaContrasenaVieja_DejaDeServir()
    {
        var (admin, usuarioId, email) = await PrepararAsync();

        await admin.PostAsync($"/api/admin/usuarios/{usuarioId}/clave-temporal", null);

        var login = await _factory.CreateClient()
            .PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "ClaveSegura#2026"));

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    /// <summary>
    /// Si alguien ya estaba adentro con la contraseña vieja, cambiarla sin
    /// sacarlo no sirve de nada: seguiría con sesión válida 90 días.
    /// </summary>
    [Fact]
    public async Task SeCierranLasSesionesQueYaEstabanAbiertas()
    {
        var (admin, _) = await _factory.CrearAdminAutenticadoAsync();
        var email = $"{Guid.NewGuid():N}@test.moneyback";
        var (_, usuario) = await _factory.CrearClienteAutenticadoAsync(email);

        // Una sesión de verdad, con su refresh token guardado.
        var login = await _factory.CreateClient()
            .PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "ClaveSegura#2026"));
        var sesion = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;

        var respuesta = await admin.PostAsync($"/api/admin/usuarios/{usuario.Id}/clave-temporal", null);
        var resultado = (await respuesta.Content.ReadFromJsonAsync<ClaveTemporalResponse>())!;
        Assert.True(resultado.SesionesCerradas >= 1);

        var refrescar = await _factory.CreateClient()
            .PostAsJsonAsync("/api/auth/refrescar", new RefrescarTokenRequest(sesion.RefreshToken));

        Assert.NotEqual(HttpStatusCode.OK, refrescar.StatusCode);
    }

    /// <summary>
    /// Después de un reajuste hecho por otra persona ya no se sabe quién
    /// tiene los códigos de recuperación viejos: dejarlos vivos sería dejar
    /// una segunda llave puesta.
    /// </summary>
    [Fact]
    public async Task LosCodigosDeRecuperacionViejosQuedanInservibles()
    {
        var (admin, _) = await _factory.CrearAdminAutenticadoAsync();
        var email = $"{Guid.NewGuid():N}@test.moneyback";
        var (cliente, usuario) = await _factory.CrearClienteAutenticadoAsync(email);

        var generar = await cliente.PostAsync("/api/auth/codigos-recuperacion", null);
        var codigos = (await generar.Content.ReadFromJsonAsync<CodigosRecuperacionResponse>())!.Codigos;

        await admin.PostAsync($"/api/admin/usuarios/{usuario.Id}/clave-temporal", null);

        var intento = await _factory.CreateClient().PostAsJsonAsync("/api/auth/recuperar",
            new RecuperarCuentaRequest(email, codigos[0], "OtraClave#2026"));

        Assert.Equal(HttpStatusCode.BadRequest, intento.StatusCode);
    }

    [Fact]
    public async Task UnUsuarioNormalNoPuedeCambiarleLaClaveANadie()
    {
        var (cliente, _) = await _factory.CrearClienteAutenticadoAsync();
        var (_, victima) = await _factory.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsync($"/api/admin/usuarios/{victima.Id}/clave-temporal", null);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task SinSesion_TampocoSePuede()
    {
        var (_, victima) = await _factory.CrearClienteAutenticadoAsync();

        var respuesta = await _factory.CreateClient()
            .PostAsync($"/api/admin/usuarios/{victima.Id}/clave-temporal", null);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task UnaCuentaQueNoExiste_DaNoEncontrado()
    {
        var (admin, _) = await _factory.CrearAdminAutenticadoAsync();

        var respuesta = await admin.PostAsync("/api/admin/usuarios/999999/clave-temporal", null);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    /// <summary>
    /// La clave se arma al azar pero tiene que cumplir las reglas de
    /// Identity siempre, no la mayoría de las veces: si falla una de cada
    /// veinte, falla justo el día que hace falta.
    /// </summary>
    [Fact]
    public async Task LaClaveGeneradaSiempreCumpleLasReglasYNuncaSeRepite()
    {
        var (admin, _) = await _factory.CrearAdminAutenticadoAsync();
        var (_, usuario) = await _factory.CrearClienteAutenticadoAsync();

        var vistas = new HashSet<string>();
        for (var i = 0; i < 25; i++)
        {
            var respuesta = await admin.PostAsync($"/api/admin/usuarios/{usuario.Id}/clave-temporal", null);
            respuesta.EnsureSuccessStatusCode();
            var clave = (await respuesta.Content.ReadFromJsonAsync<ClaveTemporalResponse>())!.Clave;
            vistas.Add(clave);
        }

        Assert.Equal(25, vistas.Count);
        // Sin caracteres que se confundan al leerla de una pantalla ajena.
        Assert.All(vistas, c => Assert.DoesNotContain(c, "0OIl1"));
    }
}
