using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using MoneyBack.Api.Data;
using MoneyBack.Api.Dtos;
using MoneyBack.Api.Models.Notificaciones;

namespace MoneyBack.Api.Tests;

/// <summary>
/// El registro de avisos. Existe porque un push que no se ve se pierde: si
/// el teléfono estaba en silencio, el aviso de que un tope se pasó nunca
/// ocurrió para esa persona.
/// </summary>
public class NotificacionesTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public NotificacionesTests(ApiFactory factory) => _factory = factory;

    private async Task SembrarAsync(int usuarioId, params (string Titulo, TipoNotificacion Tipo, bool Leida)[] avisos)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var reloj = DateTime.UtcNow.AddHours(-avisos.Length);

        foreach (var (titulo, tipo, leida) in avisos)
        {
            reloj = reloj.AddHours(1);
            db.Notificaciones.Add(new Notificacion
            {
                UsuarioId = usuarioId,
                Tipo = tipo,
                Titulo = titulo,
                Cuerpo = "Cuerpo de prueba",
                CreadaEn = reloj,
                LeidaEn = leida ? reloj : null
            });
        }
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task LaBandejaTraeLoMasRecienteArribaYCuentaLasSinLeer()
    {
        var (cliente, usuario) = await _factory.CrearClienteAutenticadoAsync();
        await SembrarAsync(usuario.Id,
            ("La más vieja", TipoNotificacion.Tope, true),
            ("Intermedia", TipoNotificacion.Resumen, false),
            ("La más nueva", TipoNotificacion.CobroFijo, false));

        var bandeja = await cliente.GetFromJsonAsync<BandejaNotificacionesResponse>("/api/notificaciones");

        Assert.NotNull(bandeja);
        Assert.Equal("La más nueva", bandeja!.Notificaciones[0].Titulo);
        Assert.Equal("La más vieja", bandeja.Notificaciones[^1].Titulo);
        Assert.Equal(2, bandeja.SinLeer);
    }

    [Fact]
    public async Task SoloSeVenLasPropias()
    {
        var (mio, usuarioMio) = await _factory.CrearClienteAutenticadoAsync();
        var (_, ajeno) = await _factory.CrearClienteAutenticadoAsync();

        await SembrarAsync(usuarioMio.Id, ("Mía", TipoNotificacion.Tope, false));
        await SembrarAsync(ajeno.Id, ("SecretoDelOtro", TipoNotificacion.Tope, false));

        var bandeja = await mio.GetFromJsonAsync<BandejaNotificacionesResponse>("/api/notificaciones");

        Assert.Single(bandeja!.Notificaciones);
        Assert.Equal("Mía", bandeja.Notificaciones[0].Titulo);
        Assert.Equal(1, bandeja.SinLeer);
    }

    [Fact]
    public async Task MarcarLeidaBajaElContador()
    {
        var (cliente, usuario) = await _factory.CrearClienteAutenticadoAsync();
        await SembrarAsync(usuario.Id, ("Un aviso", TipoNotificacion.Tope, false));

        var antes = await cliente.GetFromJsonAsync<BandejaNotificacionesResponse>("/api/notificaciones");
        var id = antes!.Notificaciones[0].Id;

        var respuesta = await cliente.PostAsync($"/api/notificaciones/{id}/leer", null);
        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);

        var despues = await cliente.GetFromJsonAsync<BandejaNotificacionesResponse>("/api/notificaciones");
        Assert.Equal(0, despues!.SinLeer);
        Assert.True(despues.Notificaciones[0].Leida);
    }

    [Fact]
    public async Task NadieMarcaComoLeidaLaNotificacionDeOtro()
    {
        var (_, dueno) = await _factory.CrearClienteAutenticadoAsync();
        var (intruso, _) = await _factory.CrearClienteAutenticadoAsync();
        await SembrarAsync(dueno.Id, ("Privada", TipoNotificacion.Tope, false));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var id = db.Notificaciones.First(n => n.UsuarioId == dueno.Id && n.Titulo == "Privada").Id;

        var respuesta = await intruso.PostAsync($"/api/notificaciones/{id}/leer", null);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task MarcarTodasDejaLaBandejaEnCero()
    {
        var (cliente, usuario) = await _factory.CrearClienteAutenticadoAsync();
        await SembrarAsync(usuario.Id,
            ("Una", TipoNotificacion.Tope, false),
            ("Otra", TipoNotificacion.Resumen, false),
            ("Ya leída", TipoNotificacion.CobroFijo, true));

        await cliente.PostAsync("/api/notificaciones/leer-todas", null);

        var bandeja = await cliente.GetFromJsonAsync<BandejaNotificacionesResponse>("/api/notificaciones");
        Assert.Equal(0, bandeja!.SinLeer);
        Assert.All(bandeja.Notificaciones, n => Assert.True(n.Leida));
    }

    /// <summary>
    /// Volver a abrir el panel no puede mover la fecha de la primera
    /// lectura: es el único dato que dice cuándo se enteró la persona.
    /// </summary>
    [Fact]
    public async Task VolverAMarcarLeidaNoCambiaLaFecha()
    {
        var (cliente, usuario) = await _factory.CrearClienteAutenticadoAsync();
        await SembrarAsync(usuario.Id, ("Un aviso", TipoNotificacion.Tope, false));

        var bandeja = await cliente.GetFromJsonAsync<BandejaNotificacionesResponse>("/api/notificaciones");
        var id = bandeja!.Notificaciones[0].Id;

        await cliente.PostAsync($"/api/notificaciones/{id}/leer", null);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var primera = db.Notificaciones.First(n => n.Id == id).LeidaEn;

        await Task.Delay(20);
        await cliente.PostAsync($"/api/notificaciones/{id}/leer", null);

        using var scope2 = _factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(primera, db2.Notificaciones.First(n => n.Id == id).LeidaEn);
    }

    [Fact]
    public async Task SinSesion_NoSeVeNada()
    {
        var respuesta = await _factory.CreateClient().GetAsync("/api/notificaciones");
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    /// <summary>
    /// Lo importante del diseño: nadie tuvo que cambiar sus llamadas. Los
    /// seis sitios que ya mandaban push ganaron historial porque el registro
    /// se hace dentro del propio envío.
    /// </summary>
    [Fact]
    public async Task MandarUnPushDejaRegistroAunqueNoHayaDispositivos()
    {
        var (cliente, usuario) = await _factory.CrearClienteAutenticadoAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<MoneyBack.Api.Services.PushNotificationSender>();
            await sender.EnviarATodosLosDispositivosAsync(
                usuario.Id, "Te pasaste del tope", "Mercado va en $520.000 de $500.000.",
                "/presupuestos", TipoNotificacion.Tope);
        }

        var bandeja = await cliente.GetFromJsonAsync<BandejaNotificacionesResponse>("/api/notificaciones");

        Assert.Single(bandeja!.Notificaciones);
        Assert.Equal("Te pasaste del tope", bandeja.Notificaciones[0].Titulo);
        Assert.Equal("Tope", bandeja.Notificaciones[0].Tipo);
        Assert.Equal("/presupuestos", bandeja.Notificaciones[0].Url);
        Assert.False(bandeja.Notificaciones[0].Leida);
    }
}
