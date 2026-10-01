using System.Net;
using MoneyBack.Web.Models;
using MoneyBack.Web.Pages;
using MoneyBack.Web.Tests.Infra;

namespace MoneyBack.Web.Tests;

public class AdminPantallaTests : PruebaDePantalla
{
    private static CuentaAdminResponse Cuenta(
        int id, string nombre, bool admin = false, bool mia = false,
        int sesiones = 1, int movimientos = 12, int grupos = 1) =>
        new(id, nombre, $"{nombre.ToLowerInvariant()}@ejemplo.com", DateTime.UtcNow.AddMonths(-3),
            admin ? ["SuperAdmin"] : [], sesiones, movimientos, grupos, DateTime.UtcNow.AddHours(-5), mia);

    private static ResumenAdminResponse Resumen(
        DateTime? ultimoAtajo = null, bool atajoBien = true, int fallidas = 0, int sinClasificar = 0) =>
        new(3, 1, 2, 240, sinClasificar, 2, 3, ultimoAtajo, atajoBien, fallidas, 0);

    private void ConDatos(ResumenAdminResponse resumen, params CuentaAdminResponse[] cuentas) => Servidor
        .Responde("GET", "api/admin/resumen", resumen)
        .Responde("GET", "api/admin/usuarios", cuentas);

    // ---------- Salud del sistema ----------

    /// <summary>
    /// El atajo del banco es el corazón de la app y su falla es silenciosa:
    /// nadie se entera hasta que cuadra cuentas. Tiene que verse de entrada.
    /// </summary>
    [Fact]
    public void ElAtajoSanoSeVeSanoYDiceCuandoFueLaUltimaVez()
    {
        ConDatos(Resumen(ultimoAtajo: DateTime.UtcNow.AddHours(-3)), Cuenta(1, "Ana", admin: true, mia: true));

        var pantalla = RenderComponent<Admin>();

        Assert.Contains("Gastos que entran solos", pantalla.Markup);
        Assert.Contains("hace 3 horas", pantalla.Markup);
        Assert.Contains("entró bien", pantalla.Markup);
    }

    [Fact]
    public void SiElAtajoNuncaLlamo_LoDiceSinRodeos()
    {
        ConDatos(Resumen(ultimoAtajo: null), Cuenta(1, "Ana", admin: true, mia: true));

        var pantalla = RenderComponent<Admin>();

        Assert.Contains("nunca ha llamado", pantalla.Markup);
        Assert.Contains("no están entrando solos", pantalla.Markup);
    }

    /// <summary>
    /// 36 horas y no 24: un día sin comprar nada es normal y no significa
    /// que la automatización esté rota.
    /// </summary>
    [Fact]
    public void UnDiaSinComprarNoSeReportaComoFalla()
    {
        ConDatos(Resumen(ultimoAtajo: DateTime.UtcNow.AddHours(-30)), Cuenta(1, "Ana", admin: true, mia: true));

        var pantalla = RenderComponent<Admin>();

        Assert.DoesNotContain("alerta", pantalla.Find(".tile-ancha .tile-dato").ClassName);
    }

    [Fact]
    public void DosDiasSinLlamar_SiSeMarcaComoProblema()
    {
        ConDatos(Resumen(ultimoAtajo: DateTime.UtcNow.AddHours(-50)), Cuenta(1, "Ana", admin: true, mia: true));

        var pantalla = RenderComponent<Admin>();

        Assert.Contains("alerta", pantalla.Find(".tile-ancha .tile-dato").ClassName);
    }

    [Fact]
    public void LosMovimientosSinClasificarSeResaltan()
    {
        ConDatos(Resumen(ultimoAtajo: DateTime.UtcNow, sinClasificar: 7), Cuenta(1, "Ana", admin: true, mia: true));

        var pantalla = RenderComponent<Admin>();

        Assert.Contains("7 sin clasificar", pantalla.Markup);
    }

    // ---------- Cuentas ----------

    [Fact]
    public void CadaCuentaMuestraSuRolYSuActividad()
    {
        ConDatos(Resumen(ultimoAtajo: DateTime.UtcNow),
            Cuenta(1, "Ana", admin: true, mia: true),
            Cuenta(2, "Luis", movimientos: 48, grupos: 2));

        var pantalla = RenderComponent<Admin>();

        Assert.Contains("administra", pantalla.Markup);
        Assert.Contains("esta es la tuya", pantalla.Markup);
        Assert.Contains("48 movimientos", pantalla.Markup);
        Assert.Contains("2 grupos", pantalla.Markup);
    }

    /// <summary>
    /// Las acciones peligrosas viven detrás de un toque, no sueltas en la
    /// lista: con cuatro cuentas en pantalla, ocho botones de borrar a la
    /// vista son un accidente esperando.
    /// </summary>
    [Fact]
    public void LasAccionesEstanEscondidasHastaQueSeAbreLaCuenta()
    {
        ConDatos(Resumen(ultimoAtajo: DateTime.UtcNow),
            Cuenta(1, "Ana", admin: true, mia: true), Cuenta(2, "Luis"));

        var pantalla = RenderComponent<Admin>();
        Assert.DoesNotContain("Borrar cuenta", pantalla.Markup);

        pantalla.FindAll("button[title='Ver acciones']")[1].Click();
        Assert.Contains("Borrar cuenta", pantalla.Markup);
    }

    [Fact]
    public void NadieSePuedeBorrarNiQuitarseLaAdministracionASiMismo()
    {
        ConDatos(Resumen(ultimoAtajo: DateTime.UtcNow), Cuenta(1, "Ana", admin: true, mia: true));

        var pantalla = RenderComponent<Admin>();
        pantalla.Find("button[title='Ver acciones']").Click();

        var borrar = pantalla.FindAll("button").First(b => b.TextContent.Contains("No puedes borrar tu cuenta"));
        var quitar = pantalla.FindAll("button").First(b => b.TextContent.Contains("No puedes quitártela"));

        Assert.True(borrar.HasAttribute("disabled"));
        Assert.True(quitar.HasAttribute("disabled"));
    }

    [Fact]
    public void AUnaCuentaSinAdministracionSeLeOfreceDarsela()
    {
        ConDatos(Resumen(ultimoAtajo: DateTime.UtcNow),
            Cuenta(1, "Ana", admin: true, mia: true), Cuenta(2, "Luis"));

        var pantalla = RenderComponent<Admin>();
        pantalla.FindAll("button[title='Ver acciones']")[1].Click();

        Assert.Contains("Dar administración", pantalla.Markup);
        Assert.DoesNotContain("Quitar administración", pantalla.Markup);
    }

    [Fact]
    public void QuitarLaAdministracionPreguntaYExplicaLoDeLasSesiones()
    {
        ConDatos(Resumen(ultimoAtajo: DateTime.UtcNow),
            Cuenta(1, "Ana", admin: true, mia: true), Cuenta(2, "Luis", admin: true));

        var pantalla = RenderComponent<Admin>();
        pantalla.FindAll("button[title='Ver acciones']")[1].Click();
        pantalla.FindAll("button").First(b => b.TextContent.Contains("Quitar administración")).Click();

        Assert.NotNull(Confirmacion.Pendiente);
        Assert.Contains("Luis", Confirmacion.Pendiente!.Titulo);
        Assert.Contains("cierran las sesiones", Confirmacion.Pendiente.Detalle);
    }

    // ---------- Borrado ----------

    private static QueSeBorrariaResponse Previa(
        string nombre = "Luis", int movimientos = 12, string? bloqueo = null,
        List<string>? seBorran = null, List<string>? sale = null) =>
        new(nombre, $"{nombre.ToLowerInvariant()}@ejemplo.com", movimientos, 5, 0, 1, 0, 2, 3, 4,
            seBorran ?? [], sale ?? [], bloqueo);

    /// <summary>
    /// Un "eliminar cuenta" sin números no dice si son cuatro movimientos de
    /// prueba o el historial de dos años de alguien.
    /// </summary>
    [Fact]
    public void BorrarMuestraPrimeroQueSeVaAPerder()
    {
        ConDatos(Resumen(ultimoAtajo: DateTime.UtcNow),
            Cuenta(1, "Ana", admin: true, mia: true), Cuenta(2, "Luis"));
        Servidor.Responde("GET", "api/admin/usuarios/2/que-se-borraria",
            Previa(movimientos: 240, seBorran: ["Ahorro personal"]));

        var pantalla = RenderComponent<Admin>();
        pantalla.FindAll("button[title='Ver acciones']")[1].Click();
        pantalla.FindAll("button").First(b => b.TextContent.Contains("Borrar cuenta")).Click();

        pantalla.WaitForAssertion(() => Assert.NotNull(Confirmacion.Pendiente));
        Assert.Contains("240 movimientos", Confirmacion.Pendiente!.Detalle);
        Assert.Contains("Ahorro personal", Confirmacion.Pendiente.Detalle);
        Assert.Contains("No se puede deshacer", Confirmacion.Pendiente.Detalle);
        Assert.DoesNotContain(Servidor.Llamadas, l => l.Metodo == "DELETE");
    }

    /// <summary>
    /// Si el servidor dice que no se puede, se muestra el motivo y no se
    /// llega siquiera a preguntar: preguntar por algo que va a fallar es
    /// hacerle perder el tiempo a alguien.
    /// </summary>
    [Fact]
    public void SiElServidorLoBloquea_SeMuestraElMotivoYNoSePregunta()
    {
        ConDatos(Resumen(ultimoAtajo: DateTime.UtcNow),
            Cuenta(1, "Ana", admin: true, mia: true), Cuenta(2, "Luis"));
        Servidor.Responde("GET", "api/admin/usuarios/2/que-se-borraria",
            Previa(bloqueo: "Tiene aportes en metas compartidas con otras personas."));

        var pantalla = RenderComponent<Admin>();
        pantalla.FindAll("button[title='Ver acciones']")[1].Click();
        pantalla.FindAll("button").First(b => b.TextContent.Contains("Borrar cuenta")).Click();

        pantalla.WaitForAssertion(() => Assert.Contains("metas compartidas", pantalla.Markup));
        Assert.Null(Confirmacion.Pendiente);
        Assert.DoesNotContain(Servidor.Llamadas, l => l.Metodo == "DELETE");
    }

    [Fact]
    public void AlConfirmar_SiBorra()
    {
        ConDatos(Resumen(ultimoAtajo: DateTime.UtcNow),
            Cuenta(1, "Ana", admin: true, mia: true), Cuenta(2, "Luis"));
        Servidor
            .Responde("GET", "api/admin/usuarios/2/que-se-borraria", Previa())
            .Responde("DELETE", "api/admin/usuarios/2", null, HttpStatusCode.NoContent);

        var pantalla = RenderComponent<Admin>();
        pantalla.FindAll("button[title='Ver acciones']")[1].Click();
        pantalla.FindAll("button").First(b => b.TextContent.Contains("Borrar cuenta")).Click();
        pantalla.WaitForAssertion(() => Assert.NotNull(Confirmacion.Pendiente));
        Confirmacion.Responder(true);

        pantalla.WaitForAssertion(() =>
            Assert.Contains(Servidor.Llamadas, l => l is { Metodo: "DELETE", Ruta: "api/admin/usuarios/2" }));
    }

    [Fact]
    public void SinNadaGuardado_LaConfirmacionLoDiceAsi()
    {
        ConDatos(Resumen(ultimoAtajo: DateTime.UtcNow),
            Cuenta(1, "Ana", admin: true, mia: true), Cuenta(2, "Luis"));
        Servidor.Responde("GET", "api/admin/usuarios/2/que-se-borraria",
            new QueSeBorrariaResponse("Luis", "luis@ejemplo.com", 0, 0, 0, 0, 0, 0, 0, 0, [], [], null));

        var pantalla = RenderComponent<Admin>();
        pantalla.FindAll("button[title='Ver acciones']")[1].Click();
        pantalla.FindAll("button").First(b => b.TextContent.Contains("Borrar cuenta")).Click();

        pantalla.WaitForAssertion(() => Assert.NotNull(Confirmacion.Pendiente));
        Assert.Contains("no tiene nada guardado", Confirmacion.Pendiente!.Detalle);
    }

    // ---------- Contraseñas ----------

    [Fact]
    public void CambiarLaClaveMuestraLaNuevaUnaSolaVez()
    {
        ConDatos(Resumen(ultimoAtajo: DateTime.UtcNow),
            Cuenta(1, "Ana", admin: true, mia: true), Cuenta(2, "Luis"));
        Servidor.Responde("POST", "api/admin/usuarios/2/clave-temporal",
            new ClaveTemporalResponse("K7M2p-Qx4Az", 2));

        var pantalla = RenderComponent<Admin>();
        pantalla.FindAll("button[title='Ver acciones']")[1].Click();
        pantalla.FindAll("button").First(b => b.TextContent.Contains("Cambiar contraseña")).Click();
        ConfirmarLoQuePregunte();

        pantalla.WaitForAssertion(() => Assert.Contains("K7M2p-Qx4Az", pantalla.Markup));
        Assert.Contains("no se puede volver a ver", pantalla.Markup);
    }

    [Fact]
    public void SinSesionesAbiertas_NoSeOfreceCerrarlas()
    {
        ConDatos(Resumen(ultimoAtajo: DateTime.UtcNow),
            Cuenta(1, "Ana", admin: true, mia: true), Cuenta(2, "Luis", sesiones: 0));

        var pantalla = RenderComponent<Admin>();
        pantalla.FindAll("button[title='Ver acciones']")[1].Click();

        var boton = pantalla.FindAll("button").First(b => b.TextContent.Contains("No tiene sesiones abiertas"));
        Assert.True(boton.HasAttribute("disabled"));
    }
}
