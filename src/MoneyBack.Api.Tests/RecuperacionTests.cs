using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MoneyBack.Api.Data;
using MoneyBack.Api.Dtos;
using MoneyBack.Api.Models.Auth;

namespace MoneyBack.Api.Tests;

/// <summary>
/// Recuperar la cuenta sin correo ni administrador. Es la diferencia entre
/// olvidar la contraseña un domingo y resolverlo en un minuto, o quedarse
/// por fuera de sus propias finanzas hasta que alguien con acceso a la base
/// tenga tiempo.
/// </summary>
public class RecuperacionTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private const string ClaveOriginal = "ClaveSegura#2026";
    private const string ClaveNueva = "OtraClaveSegura#2027";

    public RecuperacionTests(ApiFactory factory) => _factory = factory;

    private async Task<(HttpClient Cliente, string Email, List<string> Codigos)> PrepararAsync()
    {
        var cliente = _factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@test.moneyback";

        var codigoInvitacion = $"inv-{Guid.NewGuid():N}";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.CodigosInvitacion.Add(new CodigoInvitacion
            {
                CodigoHash = MoneyBack.Api.Services.TokenService.HashearToken(codigoInvitacion)
            });
            await db.SaveChangesAsync();
        }

        var registro = await cliente.PostAsJsonAsync("/api/auth/register",
            new RegistrarUsuarioRequest("Prueba", email, ClaveOriginal, codigoInvitacion));
        registro.EnsureSuccessStatusCode();

        var auth = (await registro.Content.ReadFromJsonAsync<AuthResponse>())!;
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", auth.AccessToken);

        var respuesta = await cliente.PostAsync("/api/auth/codigos-recuperacion", null);
        respuesta.EnsureSuccessStatusCode();
        var codigos = (await respuesta.Content.ReadFromJsonAsync<CodigosRecuperacionResponse>())!.Codigos;

        return (cliente, email, codigos);
    }

    private async Task<HttpResponseMessage> RecuperarAsync(string email, string codigo, string clave)
    {
        var anon = _factory.CreateClient();
        return await anon.PostAsJsonAsync("/api/auth/recuperar",
            new RecuperarCuentaRequest(email, codigo, clave));
    }

    private async Task<bool> PuedeEntrarAsync(string email, string clave)
    {
        var anon = _factory.CreateClient();
        var respuesta = await anon.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, clave));
        return respuesta.IsSuccessStatusCode;
    }

    [Fact]
    public async Task ConUnaClaveDeRecuperacionSePuedeVolverAEntrar()
    {
        var (_, email, codigos) = await PrepararAsync();

        var respuesta = await RecuperarAsync(email, codigos[0], ClaveNueva);
        respuesta.EnsureSuccessStatusCode();

        Assert.True(await PuedeEntrarAsync(email, ClaveNueva));
        Assert.False(await PuedeEntrarAsync(email, ClaveOriginal));
    }

    /// <summary>
    /// Si sirviera siempre, una foto vieja de la lista abriría la cuenta
    /// para siempre.
    /// </summary>
    [Fact]
    public async Task UnaClaveSirveUnaSolaVez()
    {
        var (_, email, codigos) = await PrepararAsync();

        (await RecuperarAsync(email, codigos[0], ClaveNueva)).EnsureSuccessStatusCode();

        var segunda = await RecuperarAsync(email, codigos[0], "TerceraClave#2028");
        Assert.Equal(HttpStatusCode.BadRequest, segunda.StatusCode);

        // Pero las otras de la lista siguen sirviendo.
        (await RecuperarAsync(email, codigos[1], "TerceraClave#2028")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task SeAceptaElCodigoComoSeaQueLoEscriban()
    {
        var (_, email, codigos) = await PrepararAsync();

        var desordenado = $"  {codigos[0].Replace("-", " ").ToLowerInvariant()}  ";
        (await RecuperarAsync(email, desordenado, ClaveNueva)).EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Recuperar la cuenta no sirve de nada si quien entró con la contraseña
    /// vieja sigue adentro.
    /// </summary>
    [Fact]
    public async Task RecuperarCierraLasSesionesAbiertas()
    {
        var (cliente, email, codigos) = await PrepararAsync();

        // Con la sesión viva, algo autenticado funciona.
        (await cliente.GetAsync("/api/auth/me")).EnsureSuccessStatusCode();

        (await RecuperarAsync(email, codigos[0], ClaveNueva)).EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var vivos = await db.RefreshTokens.CountAsync(t =>
            t.Usuario.Email == email && t.RevocadoEn == null);

        Assert.Equal(0, vivos);
    }

    [Fact]
    public async Task UnaClaveInventadaNoSirve()
    {
        var (_, email, _) = await PrepararAsync();

        var respuesta = await RecuperarAsync(email, "XXXX-XXXX", ClaveNueva);
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.True(await PuedeEntrarAsync(email, ClaveOriginal));
    }

    /// <summary>
    /// La respuesta es idéntica exista o no la cuenta: si fuera distinta,
    /// esto serviría para averiguar qué correos están registrados.
    /// </summary>
    [Fact]
    public async Task NoRevelaSiUnCorreoEstaRegistrado()
    {
        var (_, email, _) = await PrepararAsync();

        var conCuenta = await RecuperarAsync(email, "XXXX-XXXX", ClaveNueva);
        var sinCuenta = await RecuperarAsync("nadie@test.moneyback", "XXXX-XXXX", ClaveNueva);

        Assert.Equal(conCuenta.StatusCode, sinCuenta.StatusCode);

        // Se comparan los errores, no el cuerpo crudo: ProblemDetails trae un
        // traceId distinto en cada respuesta y eso no revela nada.
        static async Task<string> ErroresDe(HttpResponseMessage r)
        {
            var problema = await r.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            return problema.GetProperty("errors").ToString();
        }

        Assert.Equal(await ErroresDe(conCuenta), await ErroresDe(sinCuenta));
    }

    /// <summary>
    /// Generar una lista nueva invalida la anterior: si no, una foto vieja
    /// seguiría sirviendo justo después de rotarlos porque se creían
    /// filtrados.
    /// </summary>
    [Fact]
    public async Task GenerarClavesNuevasInvalidaLasViejas()
    {
        var (cliente, email, viejos) = await PrepararAsync();

        var respuesta = await cliente.PostAsync("/api/auth/codigos-recuperacion", null);
        var nuevos = (await respuesta.Content.ReadFromJsonAsync<CodigosRecuperacionResponse>())!.Codigos;

        Assert.Equal(HttpStatusCode.BadRequest, (await RecuperarAsync(email, viejos[0], ClaveNueva)).StatusCode);
        (await RecuperarAsync(email, nuevos[0], ClaveNueva)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task SeSabeCuantasClavesQuedan()
    {
        var (cliente, email, codigos) = await PrepararAsync();

        var antes = (await cliente.GetFromJsonAsync<CodigosRestantesResponse>("/api/auth/codigos-recuperacion/cuantos-quedan"))!;
        Assert.Equal(CodigoRecuperacion.CuantosGenerar, antes.Quedan);

        var respuesta = await RecuperarAsync(email, codigos[0], ClaveNueva);
        var resultado = (await respuesta.Content.ReadFromJsonAsync<RecuperacionExitosaResponse>())!;

        Assert.Equal(CodigoRecuperacion.CuantosGenerar - 1, resultado.CodigosQueQuedan);
    }
}
