using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MoneyBack.Api.Data;
using MoneyBack.Api.Dtos;

namespace MoneyBack.Api.Tests;

/// <summary>
/// Cerrarle la sesión a alguien que no hizo nada malo es de los errores que
/// más desgastan: obliga a escribir la contraseña en el celular justo cuando
/// uno quería anotar un gasto de treinta segundos.
///
/// El API rota el refresh token en cada uso y trata el reuso como robo. Eso
/// está bien, pero le pasaba a clientes honestos todo el tiempo: el servidor
/// rota y responde, y si el teléfono se suspende en ese instante, el token
/// nuevo nunca se guarda.
/// </summary>
public class SesionQueNoSeCierraTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public SesionQueNoSeCierraTests(ApiFactory factory) => _factory = factory;

    private async Task<(HttpClient Cliente, string Refresh)> RegistrarAsync()
    {
        var cliente = _factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@test.moneyback";

        var respuesta = await cliente.PostAsJsonAsync("/api/auth/register",
            new RegistrarUsuarioRequest("Prueba", email, "ClaveSegura#2026", await CodigoAsync()));
        respuesta.EnsureSuccessStatusCode();

        var auth = (await respuesta.Content.ReadFromJsonAsync<AuthResponse>())!;
        return (cliente, auth.RefreshToken);
    }

    /// <summary>
    /// Un código propio por registro. Reusar uno fijo choca con lo que hayan
    /// dejado otras pruebas en la misma base compartida.
    /// </summary>
    private async Task<string> CodigoAsync()
    {
        var codigo = $"prueba-{Guid.NewGuid():N}";

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        db.CodigosInvitacion.Add(new MoneyBack.Api.Models.Auth.CodigoInvitacion
        {
            CodigoHash = MoneyBack.Api.Services.TokenService.HashearToken(codigo)
        });
        await db.SaveChangesAsync();

        return codigo;
    }

    private static async Task<HttpResponseMessage> RefrescarAsync(HttpClient cliente, string refresh) =>
        await cliente.PostAsJsonAsync("/api/auth/refresh", new RefrescarTokenRequest(refresh));

    /// <summary>
    /// El caso real: el celular se suspende justo después de que el servidor
    /// rotó el token, así que el nuevo nunca se guardó. Al volver a abrir se
    /// presenta el viejo — sin haber hecho nada malo.
    /// </summary>
    [Fact]
    public async Task SiElClienteNoAlcanzoAGuardarElTokenNuevo_LaSesionSobrevive()
    {
        var (cliente, refresh) = await RegistrarAsync();

        // Primer refresco: el servidor rota, pero el cliente "no lo recibe".
        (await RefrescarAsync(cliente, refresh)).EnsureSuccessStatusCode();

        // Vuelve a presentar el viejo.
        var reintento = await RefrescarAsync(cliente, refresh);

        Assert.True(reintento.IsSuccessStatusCode, "Un reuso inmediato debería tratarse como reintento, no como robo.");

        // Y el token que entrega sirve para seguir.
        var nuevo = (await reintento.Content.ReadFromJsonAsync<AuthResponse>())!;
        (await RefrescarAsync(cliente, nuevo.RefreshToken)).EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Pasada la ventana ya no hay explicación inocente: ahí sí se cierra
    /// todo. La protección contra robo sigue existiendo.
    /// </summary>
    [Fact]
    public async Task UnTokenViejoReusadoMuchoDespues_SiCierraLaSesion()
    {
        var (cliente, refresh) = await RegistrarAsync();
        (await RefrescarAsync(cliente, refresh)).EnsureSuccessStatusCode();

        // Se envejece la revocación más allá de la gracia.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hash = MoneyBack.Api.Services.TokenService.HashearToken(refresh);
            var token = await db.RefreshTokens.FirstAsync(t => t.TokenHash == hash);
            token.RevocadoEn = DateTime.UtcNow.AddMinutes(-10);
            await db.SaveChangesAsync();
        }

        var reintento = await RefrescarAsync(cliente, refresh);
        Assert.Equal(HttpStatusCode.Unauthorized, reintento.StatusCode);
    }

    [Fact]
    public async Task UnTokenInventadoNuncaSirve()
    {
        var cliente = _factory.CreateClient();
        var respuesta = await RefrescarAsync(cliente, "esto-no-es-un-token");
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }
}
