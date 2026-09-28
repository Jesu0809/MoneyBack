using System.Net;
using MoneyBack.Web.Models;
using MoneyBack.Web.Pages;
using MoneyBack.Web.Tests.Infra;

namespace MoneyBack.Web.Tests;

public class AdminPantallaTests : PruebaDePantalla
{
    private static UsuarioAdminResponse Cuenta(int id, string nombre, params string[] roles) =>
        new(id, nombre, $"{nombre.ToLowerInvariant()}@ejemplo.com", DateTime.UtcNow.AddMonths(-3), roles.ToList());

    private void ConCuentas(params UsuarioAdminResponse[] cuentas) =>
        Servidor.Responde("GET", "api/admin/usuarios", cuentas);

    [Fact]
    public void ListaLasCuentasConSuRol()
    {
        ConCuentas(Cuenta(1, "Ana", "SuperAdmin"), Cuenta(2, "Luis"));

        var pantalla = RenderComponent<Admin>();

        Assert.Contains("Ana", pantalla.Markup);
        Assert.Contains("SuperAdmin", pantalla.Markup);
        Assert.Contains("Luis", pantalla.Markup);
    }

    /// <summary>
    /// Cambiarle la contraseña a alguien lo deja afuera hasta que le pasen
    /// la nueva. Eso no puede pasar de un toque sin querer.
    /// </summary>
    [Fact]
    public void CambiarLaClavePreguntaPrimeroYExplicaLoQueImplica()
    {
        ConCuentas(Cuenta(2, "Luis"));

        var pantalla = RenderComponent<Admin>();
        pantalla.FindAll("button").First(b => b.TextContent.Contains("Cambiar clave")).Click();

        Assert.NotNull(Confirmacion.Pendiente);
        Assert.Contains("Luis", Confirmacion.Pendiente!.Titulo);
        Assert.Contains("no podrá entrar", Confirmacion.Pendiente.Detalle);
        Assert.DoesNotContain(Servidor.Llamadas, l => l.Ruta.Contains("clave-temporal"));
    }

    [Fact]
    public void SiSeCancela_NoLeCambiaNadaANadie()
    {
        ConCuentas(Cuenta(2, "Luis"));

        var pantalla = RenderComponent<Admin>();
        pantalla.FindAll("button").First(b => b.TextContent.Contains("Cambiar clave")).Click();
        Confirmacion.Responder(false);

        Assert.DoesNotContain(Servidor.Llamadas, l => l.Ruta.Contains("clave-temporal"));
    }

    [Fact]
    public void AlConfirmar_MuestraLaClaveUnaSolaVezYAvisaQueSeAnote()
    {
        ConCuentas(Cuenta(2, "Luis"));
        Servidor.Responde("POST", "api/admin/usuarios/2/clave-temporal",
            new ClaveTemporalResponse("K7M2p-Qx4Az", 2));

        var pantalla = RenderComponent<Admin>();
        pantalla.FindAll("button").First(b => b.TextContent.Contains("Cambiar clave")).Click();
        ConfirmarLoQuePregunte();

        pantalla.WaitForAssertion(() => Assert.Contains("K7M2p-Qx4Az", pantalla.Markup));
        Assert.Contains("no se puede volver a ver", pantalla.Markup);
        Assert.Contains("2 sesión(es)", pantalla.Markup);
    }

    /// <summary>
    /// La clave sale bajo la cuenta a la que le pertenece. Con varias
    /// cuentas en pantalla, mostrarla suelta sería la receta perfecta para
    /// mandarle a una persona la clave de otra.
    /// </summary>
    [Fact]
    public void LaClaveSaleDebajoDeLaCuentaALaQueLePertenece()
    {
        ConCuentas(Cuenta(1, "Ana", "SuperAdmin"), Cuenta(2, "Luis"), Cuenta(3, "Carmen"));
        Servidor.Responde("POST", "api/admin/usuarios/3/clave-temporal",
            new ClaveTemporalResponse("R9T4k-Ze2Bm", 0));

        var pantalla = RenderComponent<Admin>();
        pantalla.FindAll("button").Where(b => b.TextContent.Contains("Cambiar clave")).ElementAt(2).Click();
        ConfirmarLoQuePregunte();
        pantalla.WaitForAssertion(() => Assert.Contains("R9T4k-Ze2Bm", pantalla.Markup));

        // Solo aparece una vez en toda la página, y después del nombre de Carmen.
        var marcado = pantalla.Markup;
        Assert.Equal(1, marcado.Split("R9T4k-Ze2Bm").Length - 1);
        Assert.True(marcado.IndexOf("Carmen", StringComparison.Ordinal) < marcado.IndexOf("R9T4k-Ze2Bm", StringComparison.Ordinal));
    }

    [Fact]
    public void SiElServidorFalla_LoDiceYNoInventaUnaClave()
    {
        ConCuentas(Cuenta(2, "Luis"));
        Servidor.Falla("POST", "api/admin/usuarios/2/clave-temporal",
            HttpStatusCode.NotFound, "Esa cuenta ya no existe.");

        var pantalla = RenderComponent<Admin>();
        pantalla.FindAll("button").First(b => b.TextContent.Contains("Cambiar clave")).Click();
        ConfirmarLoQuePregunte();

        pantalla.WaitForAssertion(() => Assert.Contains("Esa cuenta ya no existe.", pantalla.Markup));
        Assert.DoesNotContain("no se puede volver a ver", pantalla.Markup);
    }
}
