using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using MoneyBack.Api.Dtos;
using MoneyBack.Api.Models;

namespace MoneyBack.Api.Tests;

/// <summary>
/// El teclado del teléfono le mete un espacio al final del correo sin que
/// la persona lo vea.
///
/// Le pasó a alguien invitado a la app: escribió su correo bien, el teclado
/// agregó el espacio, e Identity lo rechazó porque el espacio no es un
/// caracter válido para el nombre de usuario. El mensaje salía en inglés y
/// hablaba de un "username" que esa persona nunca escribió.
/// </summary>
public class CorreoConEspaciosTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public CorreoConEspaciosTests(ApiFactory factory) => _factory = factory;

    private async Task<string> CodigoAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MoneyBack.Api.Data.ApplicationDbContext>();
        var codigo = $"invitacion-{Guid.NewGuid():N}";
        db.CodigosInvitacion.Add(new MoneyBack.Api.Models.Auth.CodigoInvitacion
        {
            CodigoHash = MoneyBack.Api.Services.TokenService.HashearToken(codigo)
        });
        await db.SaveChangesAsync();
        return codigo;
    }

    [Fact]
    public async Task UnCorreoConEspacioAlFinalSeRegistraIgual()
    {
        var correo = $"{Guid.NewGuid():N}@gmail.com";

        var respuesta = await _factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new RegistrarUsuarioRequest("Juan Pablo", correo + " ", "ClaveSegura#2026", await CodigoAsync()));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        var usuario = await userManager.FindByEmailAsync(correo);

        Assert.NotNull(usuario);
        // Guardado limpio, no con el espacio adentro.
        Assert.Equal(correo, usuario!.Email);
        Assert.Equal(correo, usuario.UserName);
    }

    [Fact]
    public async Task YDespuesPuedeEntrarEscribiendoloConEspacioOSinEl()
    {
        var correo = $"{Guid.NewGuid():N}@gmail.com";
        await _factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new RegistrarUsuarioRequest("Juan Pablo", correo, "ClaveSegura#2026", await CodigoAsync()));

        foreach (var intento in new[] { correo, correo + " ", " " + correo })
        {
            var login = await _factory.CreateClient()
                .PostAsJsonAsync("/api/auth/login", new LoginRequest(intento, "ClaveSegura#2026"));

            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        }
    }

    [Fact]
    public async Task ElNombreTambienSeGuardaSinEspaciosDeSobra()
    {
        var correo = $"{Guid.NewGuid():N}@gmail.com";

        await _factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new RegistrarUsuarioRequest("  Juan Pablo  ", correo, "ClaveSegura#2026", await CodigoAsync()));

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();

        Assert.Equal("Juan Pablo", (await userManager.FindByEmailAsync(correo))!.Nombre);
    }

    // ---------- Los mensajes, en español ----------

    /// <summary>
    /// La regla del proyecto es que todo lo que ve el usuario va en
    /// español. Los mensajes de Identity se escapaban porque los escribe el
    /// framework, no nosotros.
    /// </summary>
    [Fact]
    public async Task ElCorreoRepetidoSeAvisaEnEspanolYSugiereQueHacer()
    {
        var correo = $"{Guid.NewGuid():N}@gmail.com";
        await _factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new RegistrarUsuarioRequest("Primero", correo, "ClaveSegura#2026", await CodigoAsync()));

        var repetido = await _factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new RegistrarUsuarioRequest("Segundo", correo, "ClaveSegura#2026", await CodigoAsync()));

        var texto = await repetido.Content.ReadAsStringAsync();

        Assert.Contains("Ya hay una cuenta con ese correo", texto);
        Assert.DoesNotContain("is already taken", texto);
    }

    [Theory]
    [InlineData("corta#1A", "al menos 10 caracteres")]
    [InlineData("sinmayusculas#1", "una letra mayúscula")]
    [InlineData("SINMINUSCULAS#1", "una letra minúscula")]
    [InlineData("SinNumeros#AAA", "un número")]
    [InlineData("SinSimbolos1234", "un símbolo")]
    public async Task LasReglasDeLaContrasenaSeExplicanEnEspanol(string clave, string esperado)
    {
        var respuesta = await _factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new RegistrarUsuarioRequest("Alguien", $"{Guid.NewGuid():N}@gmail.com", clave, await CodigoAsync()));

        var texto = await respuesta.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains(esperado, texto);
    }

    /// <summary>
    /// Ningún mensaje puede hablar de "username": acá nadie escribe un
    /// nombre de usuario, escriben su correo. Mandarlos a buscar un campo
    /// que no existe es peor que no decir nada.
    /// </summary>
    [Fact]
    public async Task NingunMensajeHablaDeUnNombreDeUsuario()
    {
        var respuesta = await _factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new RegistrarUsuarioRequest("Alguien", "esto no es un correo", "ClaveSegura#2026", await CodigoAsync()));

        var texto = (await respuesta.Content.ReadAsStringAsync()).ToLowerInvariant();

        Assert.DoesNotContain("username", texto);
        Assert.DoesNotContain("is invalid", texto);
        Assert.DoesNotContain("letters or digits", texto);
    }
}
