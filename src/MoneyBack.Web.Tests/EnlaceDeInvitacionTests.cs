using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using MoneyBack.Web.Pages;
using MoneyBack.Web.Tests.Infra;

namespace MoneyBack.Web.Tests;

/// <summary>
/// El enlace de invitación es lo único de la app que sale hacia afuera: se
/// manda por WhatsApp a alguien que todavía no la conoce. Si está roto, esa
/// persona ve una página de error y no vuelve a intentar.
/// </summary>
public class EnlaceDeInvitacionTests : PruebaDePantalla
{
    /// <summary>
    /// El generador apuntaba a /register y la ruta era /registro: quien
    /// recibía el enlace caía en "Not Found".
    /// </summary>
    [Fact]
    public void ElEnlaceQueSeGeneraApuntaAUnaRutaQueExiste()
    {
        var raiz = UbicarWeb();
        var perfil = File.ReadAllText(Path.Combine(raiz, "Pages", "Perfil.razor"));

        var enlace = Regex.Match(perfil, @"BaseUri\}([a-z-]+)\?invitacion=");
        Assert.True(enlace.Success, "No encontré dónde se arma el enlace de invitación.");

        var rutas = typeof(Register).Assembly.GetTypes()
            .SelectMany(t => t.GetCustomAttributes(typeof(RouteAttribute), false))
            .Cast<RouteAttribute>()
            .Select(a => a.Template)
            .ToHashSet();

        Assert.Contains("/" + enlace.Groups[1].Value, rutas);
    }

    /// <summary>
    /// Los enlaces con /register ya salieron por WhatsApp y no se pueden
    /// corregir a distancia: esa ruta tiene que seguir funcionando.
    /// </summary>
    [Fact]
    public void LaDireccionViejaSigueLlevandoAlRegistro()
    {
        var rutas = typeof(Register).GetCustomAttributes(typeof(RouteAttribute), false)
            .Cast<RouteAttribute>()
            .Select(a => a.Template)
            .ToList();

        Assert.Contains("/registro", rutas);
        Assert.Contains("/register", rutas);
    }

    /// <summary>
    /// Quien llega por el enlace no tiene que escribir el código ni verlo:
    /// un campo lleno de algo que la persona no escribió solo invita a que
    /// lo borre. Solo se le confirma que la invitación sirve.
    ///
    /// El parámetro no se pasa a mano: viene de la URL, así que la prueba
    /// navega como lo haría quien abre el enlace.
    /// </summary>
    [Theory]
    [InlineData("registro")]
    [InlineData("register")]
    public void QuienLlegaPorElEnlaceNoTieneQueEscribirElCodigo(string ruta)
    {
        Navegador.NavigateTo($"{ruta}?invitacion=inv_QZsIIYqVYJYAtjX66ZWoQJeW6cJww2K4");

        var pantalla = RenderComponent<Register>();

        Assert.Contains("Vienes con una invitación válida", pantalla.Markup);
        Assert.DoesNotContain("Código de invitación", pantalla.Markup);
    }

    /// <summary>
    /// Y sin enlace, el campo sí aparece: alguien a quien le pasaron el
    /// código suelto tiene que poder escribirlo.
    /// </summary>
    [Fact]
    public void SinEnlace_ElCampoDelCodigoSiSePide()
    {
        var pantalla = RenderComponent<Register>();

        Assert.Contains("Código de invitación", pantalla.Markup);
        Assert.DoesNotContain("Vienes con una invitación válida", pantalla.Markup);
    }

    [Fact]
    public void ElCodigoDelEnlaceSeMandaAlRegistrarse()
    {
        const string codigo = "inv_QZsIIYqVYJYAtjX66ZWoQJeW6cJww2K4";
        Navegador.NavigateTo($"registro?invitacion={codigo}");
        Servidor.Falla("POST", "api/auth/register", System.Net.HttpStatusCode.BadRequest, "No importa");

        var pantalla = RenderComponent<Register>();

        // Se vuelve a buscar el campo después de cada cambio: cada uno
        // repinta el componente y las referencias viejas quedan obsoletas.
        pantalla.FindAll("input.input")[0].Change("Alguien");
        pantalla.FindAll("input.input")[1].Change("alguien@ejemplo.com");
        pantalla.FindAll("input.input")[2].Change("ClaveLargaDeVerdad");
        pantalla.Find("form").Submit();

        var llamada = Servidor.Llamadas.Last(l => l is { Metodo: "POST", Ruta: "api/auth/register" });
        Assert.Contains(codigo, llamada.Cuerpo);
    }

    // ---------- La página de "no encontrado" ----------

    /// <summary>
    /// Era la de la plantilla de Blazor: en inglés, sin explicar nada y sin
    /// salida. Y es la primera —y quizá única— pantalla que ve alguien a
    /// quien le pasaron un enlace a medias.
    /// </summary>
    [Fact]
    public void LaPaginaDeNoEncontradoEstaEnEspanolYOfreceSalida()
    {
        var pantalla = RenderComponent<MoneyBack.Web.Pages.NotFound>();

        Assert.Contains("No encontramos esa página", pantalla.Markup);
        Assert.DoesNotContain("Sorry, the content", pantalla.Markup);
        Assert.Contains("Ir al inicio", pantalla.Markup);
        Assert.Contains("Crear una cuenta", pantalla.Markup);
    }

    [Fact]
    public void DesdeAhiSePuedeLlegarAlRegistro()
    {
        var pantalla = RenderComponent<MoneyBack.Web.Pages.NotFound>();

        pantalla.FindAll("button").First(b => b.TextContent.Contains("Crear una cuenta")).Click();

        Assert.Equal("registro", RutaActual);
    }

    /// <summary>
    /// Nada de lo que ve el usuario puede quedar en inglés. Es la regla del
    /// proyecto y acá se escapó durante meses porque era una página que
    /// nadie visitaba a propósito.
    /// </summary>
    [Fact]
    public void NingunaPantallaDejaTextoDeLaPlantillaEnIngles()
    {
        var raiz = UbicarWeb();
        var delatores = new[]
        {
            "Sorry, the content", "Not Found", "There was an error",
            "Loading...", "An unhandled error"
        };

        var encontrados = new List<string>();
        foreach (var archivo in Directory.EnumerateFiles(Path.Combine(raiz, "Pages"), "*.razor"))
        {
            // Sin los comentarios: explicar en un comentario que algo decía
            // "Not Found" no es dejarlo en pantalla.
            var texto = Regex.Replace(File.ReadAllText(archivo), @"@\*.*?\*@", "", RegexOptions.Singleline);
            texto = Regex.Replace(texto, @"^\s*//.*$", "", RegexOptions.Multiline);

            foreach (var frase in delatores.Where(texto.Contains))
            {
                encontrados.Add($"{Path.GetFileName(archivo)}: \"{frase}\"");
            }
        }

        Assert.Empty(encontrados);
    }

    private static string UbicarWeb()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "MoneyBack.Web")))
        {
            dir = dir.Parent;
        }
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "src", "MoneyBack.Web");
    }
}
