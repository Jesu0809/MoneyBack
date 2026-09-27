using System.Net;
using Microsoft.Extensions.DependencyInjection;
using MoneyBack.Web.Models;
using MoneyBack.Web.Pages;
using MoneyBack.Web.Services;
using MoneyBack.Web.Tests.Infra;

namespace MoneyBack.Web.Tests;

public class NavegacionYSesionTests : PruebaDePantalla
{
    // ---------- Login ----------

    [Fact]
    public void Login_CredencialesMalas_MuestraElMotivoYNoNavega()
    {
        Servidor.Falla("POST", "api/auth/login", HttpStatusCode.Unauthorized, "Correo o contraseña incorrectos.");

        var pantalla = RenderComponent<Login>();
        pantalla.FindAll("input.input")[0].Change("alguien@ejemplo.com");
        pantalla.FindAll("input.input")[1].Change("malaclave");
        pantalla.Find("form").Submit();

        Assert.Contains("Correo o contraseña incorrectos.", pantalla.Markup);
        Assert.Equal("", RutaActual);
    }

    /// <summary>
    /// Sin conexión el login no puede decir "contraseña incorrecta": manda a
    /// la persona a cambiar una clave que estaba bien.
    /// </summary>
    [Fact]
    public void Login_SinConexion_NoCulpaALaContrasena()
    {
        Servidor.RespondeCon("POST", "api/auth/login", _ => throw new HttpRequestException("sin red"));

        var pantalla = RenderComponent<Login>();
        pantalla.FindAll("input.input")[0].Change("alguien@ejemplo.com");
        pantalla.FindAll("input.input")[1].Change("clavebuena");

        // El manejador de fallos de red convierte esto en un 503, no en un 401.
        var excepcion = Record.Exception(() => pantalla.Find("form").Submit());
        Assert.Null(excepcion);
        Assert.DoesNotContain("Correo o contraseña incorrectos", pantalla.Markup);
    }

    [Fact]
    public void Login_OfreceRecuperarLaClaveYCrearCuenta()
    {
        var pantalla = RenderComponent<Login>();

        Assert.Contains("/recuperar", pantalla.Markup);
        Assert.Contains("/registro", pantalla.Markup);
    }

    // ---------- Elegir meta ----------

    [Fact]
    public void ElegirMeta_AgrupaLasMetasPorGrupoParaDistinguirLasQueSeLlamanIgual()
    {
        Servidor
            .Responde("GET", "api/hogares", new[]
            {
                Datos.Grupo(1, "Con mi pareja"),
                Datos.Grupo(2, "Con la familia")
            })
            .Responde("GET", "api/hogares/1/metas", new[] { Datos.Meta(10, "Emergencia", hogarId: 1) })
            .Responde("GET", "api/hogares/2/metas", new[] { Datos.Meta(20, "Emergencia", hogarId: 2) });

        var pantalla = RenderComponent<ElegirMeta>();

        Assert.Contains("Con mi pareja", pantalla.Markup);
        Assert.Contains("Con la familia", pantalla.Markup);
        Assert.Equal(2, pantalla.FindAll(".card").Count);
    }

    [Fact]
    public void ElegirMeta_NoOfreceLasArchivadas()
    {
        Servidor
            .Responde("GET", "api/hogares", new[] { Datos.Grupo() })
            .Responde("GET", "api/hogares/1/metas", new[]
            {
                Datos.Meta(10, "Cuota inicial"),
                Datos.Meta(11, "Lavadora vieja", activa: false)
            });

        var pantalla = RenderComponent<ElegirMeta>();

        Assert.Contains("Cuota inicial", pantalla.Markup);
        Assert.DoesNotContain("Lavadora vieja", pantalla.Markup);
    }

    [Fact]
    public void ElegirMeta_MarcarFavoritaLlevaDirectoAAportar()
    {
        Servidor
            .Responde("GET", "api/hogares", new[] { Datos.Grupo() })
            .Responde("GET", "api/hogares/1/metas", new[] { Datos.Meta(10, "Cuota inicial") })
            .Responde("POST", "api/metas/10/favorita", new { });

        var pantalla = RenderComponent<ElegirMeta>();
        pantalla.FindAll("button").First(b => b.TextContent.Contains("vaya siempre")).Click();

        Assert.Equal("metas/10", RutaActual);
    }

    // ---------- Detalle de meta ----------

    [Fact]
    public void MetaDetalle_MuestraElProgresoYQuienAporto()
    {
        var meta = Datos.Meta(10, "Cuota inicial", actual: 12_000_000, objetivo: 48_000_000);
        Servidor.Responde("GET", "api/metas/10",
            Datos.Detalle(meta, new AportePorUsuario(1, "Ana", 7_000_000), new AportePorUsuario(2, "Luis", 5_000_000)));

        var pantalla = RenderComponent<MetaDetalle>(p => p.Add(m => m.MetaId, 10));

        Assert.Contains("Cuota inicial", pantalla.Markup);
        Assert.Contains("Ana", pantalla.Markup);
        Assert.Contains("7.000.000", pantalla.Markup);
        // El anillo arranca en cero y se llena, así que el número llega un
        // instante después del primer pintado.
        pantalla.WaitForAssertion(() => Assert.Contains("25%", pantalla.Markup));
    }

    [Fact]
    public void MetaDetalle_UnaMetaArchivadaNoDejaRegistrarAportes()
    {
        var meta = Datos.Meta(10, "Lavadora", activa: false);
        Servidor.Responde("GET", "api/metas/10", Datos.Detalle(meta));

        var pantalla = RenderComponent<MetaDetalle>(p => p.Add(m => m.MetaId, 10));

        Assert.Contains("Archivada", pantalla.Markup);
        Assert.DoesNotContain("Registrar aporte o retiro", pantalla.Markup);
    }

    [Fact]
    public void MetaDetalle_SiLaMetaNoExiste_LoDiceYOfreceVolver()
    {
        Servidor.Falla("GET", "api/metas/99", HttpStatusCode.NotFound, "No existe.");

        var pantalla = RenderComponent<MetaDetalle>(p => p.Add(m => m.MetaId, 99));

        Assert.Contains("No encontramos esta meta", pantalla.Markup);
        Assert.Contains("/hogar", pantalla.Markup);
    }

    [Fact]
    public void MetaDetalle_RegistrarUnAporteMandaTipoYMonto()
    {
        var meta = Datos.Meta(10);
        Servidor
            .Responde("GET", "api/metas/10", Datos.Detalle(meta))
            // El API devuelve el id del movimiento, un número pelado.
            .Responde("POST", "api/metas/10/movimientos", 1, HttpStatusCode.Created);

        var pantalla = RenderComponent<MetaDetalle>(p => p.Add(m => m.MetaId, 10));
        pantalla.Find("button.btn-primary").Click();
        pantalla.Find("input[type=number]").Change("500000");
        pantalla.Find("form").Submit();

        var llamada = Servidor.Llamadas.Last(l => l.Ruta == "api/metas/10/movimientos");
        Assert.Contains("\"tipo\":\"Aporte\"", llamada.Cuerpo);
        Assert.Contains("500000", llamada.Cuerpo);
    }

    // ---------- El LED del encabezado ----------

    [Fact]
    public void ElLed_EstaVerdeConConexionYSinNadaPendiente()
    {
        var estado = Services.GetRequiredService<EstadoConexion>();

        Assert.Equal(Conexion.EnLinea, estado.Actual);
        Assert.Equal("Todo al día", estado.Descripcion);
    }

    [Fact]
    public void ElLed_SinConexionYConGastosAnotados_DiceCuantosFaltanPorSubir()
    {
        var estado = Services.GetRequiredService<EstadoConexion>();

        estado.MarcarSinConexion();
        estado.ActualizarPendientes(3);

        Assert.Equal(Conexion.Registrando, estado.Actual);
        Assert.Equal("Sin conexión · 3 por subir", estado.Descripcion);
    }

    [Fact]
    public void ElLed_SinConexionYSinNadaPendiente_EsSoloSinConexion()
    {
        var estado = Services.GetRequiredService<EstadoConexion>();

        estado.MarcarSinConexion();

        Assert.Equal(Conexion.SinConexion, estado.Actual);
    }

    /// <summary>
    /// El punto vive en el layout y el estado lo cambia el manejador de red,
    /// que corre en otro hilo: si no avisara, el LED se quedaría verde para
    /// siempre — que es exactamente lo que pasaba.
    /// </summary>
    [Fact]
    public void ElLed_AvisaCuandoCambia()
    {
        var estado = Services.GetRequiredService<EstadoConexion>();
        var avisos = 0;
        estado.OnCambio += () => avisos++;

        estado.MarcarSinConexion();
        estado.ActualizarPendientes(1);
        estado.MarcarEnLinea();

        Assert.Equal(3, avisos);
    }

    /// <summary>Repetir el mismo estado no debe repintar la app entera.</summary>
    [Fact]
    public void ElLed_NoAvisaSiElEstadoNoCambio()
    {
        var estado = Services.GetRequiredService<EstadoConexion>();
        estado.MarcarSinConexion();

        var avisos = 0;
        estado.OnCambio += () => avisos++;
        estado.MarcarSinConexion();
        estado.ActualizarPendientes(0);

        Assert.Equal(0, avisos);
    }
}
