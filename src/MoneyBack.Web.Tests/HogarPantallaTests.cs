using System.Net;
using MoneyBack.Web.Models;
using MoneyBack.Web.Pages;
using MoneyBack.Web.Tests.Infra;

namespace MoneyBack.Web.Tests;

/// <summary>
/// La pantalla de grupos y metas: es la que más estados distintos tiene
/// (sin grupo, con invitación recibida, con invitación enviada, con grupo)
/// y por eso la que más fácil se rompe al tocar cualquiera de ellos.
/// </summary>
public class HogarPantallaTests : PruebaDePantalla
{
    private void SinGrupos() => Servidor
        .Responde("GET", "api/hogares", Array.Empty<object>())
        .Responde("GET", "api/invitaciones-hogar/mia", Datos.SinInvitaciones);

    [Fact]
    public void SinNingunGrupo_OfreceCrearUno()
    {
        SinGrupos();

        var pantalla = RenderComponent<Home>();

        Assert.Contains("Empieza un grupo de ahorro", pantalla.Markup);
        Assert.Contains("Crear grupo", pantalla.Markup);
    }

    /// <summary>
    /// El bloque final de la página cierra un `else` abierto arriba y una
    /// llave de más se imprime como texto en la pantalla — pasó tres veces.
    /// </summary>
    [Fact]
    public void SinNingunGrupo_NoImprimeLlavesSueltas()
    {
        SinGrupos();

        var pantalla = RenderComponent<Home>();

        Assert.DoesNotContain("}", TextoVisible(pantalla.Markup));
    }

    [Fact]
    public void ConGrupoYMetas_MuestraLaMetaYElAhorroTotal()
    {
        var meta = Datos.Meta(nombre: "Cuota inicial", actual: 5_000_000, objetivo: 50_000_000);
        Servidor
            .Responde("GET", "api/hogares", new[] { Datos.Grupo(nombre: "Nuestro grupo", miembros: ["Ana", "Luis"]) })
            .Responde("GET", "api/hogares/1/metas", new[] { meta })
            .Responde("GET", "api/metas/10", Datos.Detalle(meta, new AportePorUsuario(1, "Ana", 3_000_000)));

        var pantalla = RenderComponent<Home>();

        Assert.Contains("Nuestro grupo", pantalla.Markup);
        Assert.Contains("Cuota inicial", pantalla.Markup);
        Assert.Contains("Ana · Luis", pantalla.Markup);
        Assert.Contains("Ahorro total", pantalla.Markup);
    }

    /// <summary>
    /// Con grupo pero sin metas el formulario tiene que venir abierto: si
    /// hubiera que buscarlo, un grupo recién creado se ve como una pantalla
    /// vacía sin nada que hacer.
    /// </summary>
    [Fact]
    public void ConGrupoSinMetas_ElFormularioDeLaPrimeraMetaVieneAbierto()
    {
        Servidor
            .Responde("GET", "api/hogares", new[] { Datos.Grupo() })
            .Responde("GET", "api/hogares/1/metas", Array.Empty<object>());

        var pantalla = RenderComponent<Home>();

        Assert.Contains("Crea tu primera meta", pantalla.Markup);
    }

    [Fact]
    public void CrearGrupo_MandaElNombreQueSeEscribio()
    {
        SinGrupos();
        Servidor.Responde("POST", "api/hogares", Datos.Grupo(nombre: "Viaje"), HttpStatusCode.Created);

        var pantalla = RenderComponent<Home>();
        pantalla.Find("input.input").Change("Viaje");
        pantalla.Find("form").Submit();

        var llamada = Servidor.Llamadas.Last(l => l is { Metodo: "POST", Ruta: "api/hogares" });
        Assert.Contains("\"nombre\":\"Viaje\"", llamada.Cuerpo);
    }

    [Fact]
    public void CrearGrupo_SiElServidorLoRechaza_MuestraElMotivo()
    {
        SinGrupos();
        Servidor.Falla("POST", "api/hogares", HttpStatusCode.BadRequest, "Ese nombre ya lo usaste.");

        var pantalla = RenderComponent<Home>();
        pantalla.Find("input.input").Change("Repetido");
        pantalla.Find("form").Submit();

        Assert.Contains("Ese nombre ya lo usaste.", pantalla.Markup);
    }

    [Fact]
    public void ConInvitacionRecibida_SeVeQuienInvitaYSePuedeAceptar()
    {
        Servidor
            .Responde("GET", "api/hogares", Array.Empty<object>())
            .Responde("GET", "api/invitaciones-hogar/mia", new
            {
                recibida = new
                {
                    id = 5, invitadorId = 2, invitadorNombre = "Camila",
                    invitadoId = 1, invitadoNombre = "Ana",
                    estado = "Pendiente", fechaCreacion = DateTime.UtcNow
                },
                enviada = (object?)null
            });

        var pantalla = RenderComponent<Home>();

        Assert.Contains("Tienes una invitación", pantalla.Markup);
        Assert.Contains("Camila", pantalla.Markup);
    }

    /// <summary>
    /// Sumar gente al grupo vivía solo en la pantalla que sale cuando NO
    /// tienes grupo — o sea, era inalcanzable una vez creado.
    /// </summary>
    [Fact]
    public void ConGrupo_SePuedeAbrirElPanelParaSumarGente()
    {
        Servidor
            .Responde("GET", "api/hogares", new[] { Datos.Grupo() })
            .Responde("GET", "api/hogares/1/metas", Array.Empty<object>());

        var pantalla = RenderComponent<Home>();
        pantalla.Find("button[title='Mis grupos']").Click();

        Assert.Contains("Sumar a alguien", pantalla.Markup);
        Assert.Contains("Crear otro grupo", pantalla.Markup);
    }

    [Fact]
    public void ConVariasMetas_LasArchivadasSePuedenRecuperar()
    {
        var activa = Datos.Meta(id: 10, nombre: "Cuota inicial");
        var archivada = Datos.Meta(id: 11, nombre: "Lavadora", activa: false, actual: 300_000, objetivo: 2_000_000);
        Servidor
            .Responde("GET", "api/hogares", new[] { Datos.Grupo() })
            .Responde("GET", "api/hogares/1/metas", new[] { activa, archivada })
            .Responde("GET", "api/metas/10", Datos.Detalle(activa))
            .Responde("GET", "api/metas/11", Datos.Detalle(archivada));

        var pantalla = RenderComponent<Home>();

        Assert.Contains("Archivadas", pantalla.Markup);
        Assert.Contains("Recuperar", pantalla.Markup);
    }

    /// <summary>Quita etiquetas y atributos para mirar solo lo que se lee.</summary>
    private static string TextoVisible(string markup) =>
        System.Text.RegularExpressions.Regex.Replace(markup, "<[^>]*>", " ");
}
