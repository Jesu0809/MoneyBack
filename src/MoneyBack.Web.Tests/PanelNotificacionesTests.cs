using Microsoft.AspNetCore.Components;
using MoneyBack.Web.Layout;
using MoneyBack.Web.Models;
using MoneyBack.Web.Tests.Infra;

namespace MoneyBack.Web.Tests;

/// <summary>
/// La campanita del encabezado. Hasta ahora solo mostraba cobros por
/// confirmar; los topes, el resumen semanal y los gastos que entran solos
/// salían por push y no dejaban rastro.
/// </summary>
public class PanelNotificacionesTests : PruebaDePantalla
{
    private static NotificacionResponse Aviso(
        int id, string titulo, string tipo = "Tope", bool leida = false,
        string? url = null, int horasAtras = 2) =>
        new(id, tipo, titulo, "Detalle del aviso", url, DateTime.UtcNow.AddHours(-horasAtras), leida);

    private void ConBandeja(int sinLeer, params NotificacionResponse[] avisos) => Servidor
        .Responde("GET", "api/notificaciones", new BandejaNotificacionesResponse(avisos.ToList(), sinLeer))
        .Responde("GET", "api/suscripciones/confirmaciones-pendientes", Array.Empty<object>());

    private IRenderedComponent<MainLayout> AbrirPanel()
    {
        var layout = RenderComponent<MainLayout>(p => p.Add(l => l.Body, (RenderFragment)(b => { })));
        layout.Find("button.notif-bell").Click();
        return layout;
    }

    [Fact]
    public void LaCampanitaCuentaLoSinLeer()
    {
        ConBandeja(3, Aviso(1, "Uno"), Aviso(2, "Dos"), Aviso(3, "Tres"));

        var layout = RenderComponent<MainLayout>(p => p.Add(l => l.Body, (RenderFragment)(b => { })));

        layout.WaitForAssertion(() => Assert.Equal("3", layout.Find(".notif-badge").TextContent));
    }

    /// <summary>
    /// Lo que pide respuesta y lo que solo informa suman en la misma
    /// campanita: dos badges obligarían a entender la diferencia antes de
    /// poder mirar de reojo.
    /// </summary>
    [Fact]
    public void LaCampanitaSumaLosCobrosPorConfirmarYLosAvisosSinLeer()
    {
        Servidor
            .Responde("GET", "api/notificaciones",
                new BandejaNotificacionesResponse([Aviso(1, "Un tope")], 1))
            .Responde("GET", "api/suscripciones/confirmaciones-pendientes", new[]
            {
                new ConfirmacionPendienteResponse(1, 5, "Netflix", 38_900, "🎬", DateTime.UtcNow)
            });

        var layout = RenderComponent<MainLayout>(p => p.Add(l => l.Body, (RenderFragment)(b => { })));

        layout.WaitForAssertion(() => Assert.Equal("2", layout.Find(".notif-badge").TextContent));
    }

    [Fact]
    public void SinNadaSinLeer_NoHayGlobo()
    {
        ConBandeja(0, Aviso(1, "Ya leída", leida: true));

        var layout = RenderComponent<MainLayout>(p => p.Add(l => l.Body, (RenderFragment)(b => { })));

        layout.WaitForAssertion(() => Assert.Empty(layout.FindAll(".notif-badge")));
    }

    [Fact]
    public void ElPanelListaLosAvisosConSuAntiguedad()
    {
        ConBandeja(1, Aviso(1, "Te pasaste del tope de Mercado", horasAtras: 3));

        var layout = AbrirPanel();

        layout.WaitForAssertion(() => Assert.Contains("Te pasaste del tope de Mercado", layout.Markup));
        Assert.Contains("hace 3 horas", layout.Markup);
    }

    [Fact]
    public void LasSinLeerSeDistinguenDeLasLeidas()
    {
        ConBandeja(1, Aviso(1, "Nueva"), Aviso(2, "Vieja", leida: true));

        var layout = AbrirPanel();

        layout.WaitForAssertion(() => Assert.Contains("Nueva", layout.Markup));
        Assert.Single(layout.FindAll(".punto-sin-leer"));
    }

    [Fact]
    public void TocarUnaLaMarcaLeidaYBajaElContadorDeInmediato()
    {
        ConBandeja(1, Aviso(1, "Un aviso"));
        Servidor.Responde("POST", "api/notificaciones/1/leer", null, System.Net.HttpStatusCode.NoContent);

        var layout = AbrirPanel();
        layout.WaitForAssertion(() => Assert.Single(layout.FindAll(".punto-sin-leer")));

        layout.FindAll(".card .card-tap").First().Click();

        layout.WaitForAssertion(() => Assert.Empty(layout.FindAll(".punto-sin-leer")));
        Assert.Empty(layout.FindAll(".notif-badge"));
        Assert.Contains(Servidor.Llamadas, l => l.Ruta == "api/notificaciones/1/leer");
    }

    [Fact]
    public void UnAvisoConDestinoLlevaAhi()
    {
        ConBandeja(1, Aviso(1, "Te pasaste del tope", url: "/presupuestos"));
        Servidor.Responde("POST", "api/notificaciones/1/leer", null, System.Net.HttpStatusCode.NoContent);

        var layout = AbrirPanel();
        layout.WaitForAssertion(() => Assert.Contains("Te pasaste del tope", layout.Markup));
        layout.FindAll(".card .card-tap").First().Click();

        layout.WaitForAssertion(() => Assert.Equal("presupuestos", RutaActual));
    }

    [Fact]
    public void MarcarTodasDejaLaCampanitaEnCero()
    {
        ConBandeja(2, Aviso(1, "Una"), Aviso(2, "Otra"));
        Servidor.Responde("POST", "api/notificaciones/leer-todas", new { marcadas = 2 });

        var layout = AbrirPanel();
        layout.WaitForAssertion(() => Assert.Equal(2, layout.FindAll(".punto-sin-leer").Count));

        layout.FindAll("button").First(b => b.TextContent.Contains("Marcar todas")).Click();

        layout.WaitForAssertion(() => Assert.Empty(layout.FindAll(".punto-sin-leer")));
        Assert.Empty(layout.FindAll(".notif-badge"));
    }

    /// <summary>
    /// Una bandeja vacía tiene que explicar qué va a aparecer ahí. "No hay
    /// notificaciones" a secas se lee como que algo falló.
    /// </summary>
    [Fact]
    public void SinNada_ElPanelExplicaQueVaAAparecerAhi()
    {
        ConBandeja(0);

        var layout = AbrirPanel();

        layout.WaitForAssertion(() => Assert.Contains("Nada por ahora", layout.Markup));
        Assert.Contains("resumen de cada semana", layout.Markup);
    }

    /// <summary>
    /// Los cobros por confirmar siguen arriba y con sus botones: son lo
    /// único del panel que pide una acción.
    /// </summary>
    [Fact]
    public void LosCobrosPorConfirmarVanArribaYConservanSusBotones()
    {
        Servidor
            .Responde("GET", "api/notificaciones",
                new BandejaNotificacionesResponse([Aviso(1, "Un aviso cualquiera")], 1))
            .Responde("GET", "api/suscripciones/confirmaciones-pendientes", new[]
            {
                new ConfirmacionPendienteResponse(1, 5, "Netflix", 38_900, "🎬", DateTime.UtcNow)
            });

        var layout = AbrirPanel();

        layout.WaitForAssertion(() => Assert.Contains("Necesitan tu respuesta", layout.Markup));
        var marcado = layout.Markup;
        Assert.True(
            marcado.IndexOf("Necesitan tu respuesta", StringComparison.Ordinal)
            < marcado.IndexOf("Lo que ha pasado", StringComparison.Ordinal));
        Assert.Contains("¿Se cobró?", marcado);
    }

    /// <summary>
    /// El panel tiene que flotar anclado al encabezado, no ocupar un lugar
    /// en el flujo de la página.
    ///
    /// Como tarjeta en el flujo, tocar la campanita con la página bajada
    /// abría el panel en su posición del documento —más arriba de lo que se
    /// está viendo— y parecía que el botón no servía. Pasó en producción y
    /// desde el escritorio, sin scroll, era invisible.
    /// </summary>
    [Fact]
    public void ElPanelFlotaYNoSeAbreFueraDeLaPantalla()
    {
        ConBandeja(1, Aviso(1, "Un aviso"));

        var layout = AbrirPanel();

        layout.WaitForAssertion(() => Assert.Single(layout.FindAll(".panel-flotante")));
    }

    [Fact]
    public void TocarPorFueraCierraElPanel()
    {
        ConBandeja(1, Aviso(1, "Un aviso"));

        var layout = AbrirPanel();
        layout.WaitForAssertion(() => Assert.Single(layout.FindAll(".panel-telon")));

        layout.Find(".panel-telon").Click();

        Assert.Empty(layout.FindAll(".panel-flotante"));
        Assert.Empty(layout.FindAll(".panel-telon"));
    }

    /// <summary>
    /// Los dos paneles del encabezado no pueden estar abiertos a la vez:
    /// ocupan el mismo lugar y encimados no se lee ninguno.
    /// </summary>
    [Fact]
    public void AbrirElMenuDeLaCuentaCierraElDeNotificaciones()
    {
        ConBandeja(1, Aviso(1, "Un aviso"));

        var layout = AbrirPanel();
        layout.WaitForAssertion(() => Assert.Contains("Notificaciones", layout.Markup));

        layout.Find("button.avatar-btn").Click();

        Assert.Single(layout.FindAll(".panel-flotante"));
        Assert.Contains("Cerrar sesión", layout.Markup);
    }
}
