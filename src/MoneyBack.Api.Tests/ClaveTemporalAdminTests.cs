using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
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

    // ---------- Quitar la administración ----------

    private async Task<int> CrearOtroAdminAsync()
    {
        var (_, creado) = await _factory.CrearAdminAutenticadoAsync();
        await DarleElRolAsync(creado.Id);
        return creado.Id;
    }

    /// <summary>
    /// Vuelve a buscar la cuenta dentro de este ámbito: la instancia que
    /// devuelve el ayudante viene de otro contexto de EF y adjuntarla acá
    /// choca con la que este ya tiene.
    /// </summary>
    private async Task DarleElRolAsync(int usuarioId)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider
            .GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<MoneyBack.Api.Models.Usuario>>();
        var roleManager = scope.ServiceProvider
            .GetRequiredService<Microsoft.AspNetCore.Identity.RoleManager<Microsoft.AspNetCore.Identity.IdentityRole<int>>>();

        if (!await roleManager.RoleExistsAsync(MoneyBack.Api.Models.Auth.Roles.SuperAdmin))
        {
            await roleManager.CreateAsync(
                new Microsoft.AspNetCore.Identity.IdentityRole<int>(MoneyBack.Api.Models.Auth.Roles.SuperAdmin));
        }

        var usuario = await userManager.FindByIdAsync(usuarioId.ToString());
        await userManager.AddToRoleAsync(usuario!, MoneyBack.Api.Models.Auth.Roles.SuperAdmin);
    }

    [Fact]
    public async Task QuitarLaAdministracionSeLaQuitaDeVerdad()
    {
        var (admin, _) = await _factory.CrearAdminAutenticadoAsync();
        var otroId = await CrearOtroAdminAsync();

        var respuesta = await admin.PostAsync($"/api/admin/usuarios/{otroId}/quitar-administracion", null);
        respuesta.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider
            .GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<MoneyBack.Api.Models.Usuario>>();
        var usuario = await userManager.FindByIdAsync(otroId.ToString());

        Assert.DoesNotContain(MoneyBack.Api.Models.Auth.Roles.SuperAdmin,
            await userManager.GetRolesAsync(usuario!));
    }

    /// <summary>
    /// El rol viaja dentro del token de acceso: sin cerrar las sesiones, la
    /// cuenta seguiría administrando con el token que ya tenía.
    /// </summary>
    [Fact]
    public async Task QuitarLaAdministracionTambienCierraSusSesiones()
    {
        var (admin, _) = await _factory.CrearAdminAutenticadoAsync();
        var correo = $"{Guid.NewGuid():N}@test.moneyback";
        var (_, victima) = await _factory.CrearClienteAutenticadoAsync(correo);

        await DarleElRolAsync(victima.Id);

        var login = await _factory.CreateClient()
            .PostAsJsonAsync("/api/auth/login", new LoginRequest(correo, "ClaveSegura#2026"));
        var sesion = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;

        await admin.PostAsync($"/api/admin/usuarios/{victima.Id}/quitar-administracion", null);

        var refrescar = await _factory.CreateClient()
            .PostAsJsonAsync("/api/auth/refresh", new RefrescarTokenRequest(sesion.RefreshToken));

        Assert.NotEqual(HttpStatusCode.OK, refrescar.StatusCode);
    }

    /// <summary>
    /// Quitársela a uno mismo dejaría el sistema sin nadie que pueda
    /// devolverla: la única salida sería un secreto de Fly y un reinicio.
    /// </summary>
    [Fact]
    public async Task NadieSePuedeQuitarLaAdministracionASiMismo()
    {
        var (admin, yo) = await _factory.CrearAdminAutenticadoAsync();

        var respuesta = await admin.PostAsync($"/api/admin/usuarios/{yo.Id}/quitar-administracion", null);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task UnUsuarioNormalNoLeQuitaLaAdministracionANadie()
    {
        var (cliente, _) = await _factory.CrearClienteAutenticadoAsync();
        var otroId = await CrearOtroAdminAsync();

        var respuesta = await cliente.PostAsync($"/api/admin/usuarios/{otroId}/quitar-administracion", null);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }
}
