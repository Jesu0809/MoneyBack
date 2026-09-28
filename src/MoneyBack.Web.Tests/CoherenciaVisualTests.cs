using System.Text.RegularExpressions;

namespace MoneyBack.Web.Tests;

/// <summary>
/// Reglas que valen para toda la app y que ningún compilador revisa. Todas
/// nacen de errores que ya pasaron: un ícono con el nombre mal escrito sale
/// como un círculo vacío, una variable de color inexistente anula la
/// declaración entera y el borde simplemente no se dibuja. Nada de eso
/// falla al compilar ni se ve en una revisión rápida.
/// </summary>
public class CoherenciaVisualTests
{
    private static readonly string Raiz = UbicarProyectoWeb();

    private static string UbicarProyectoWeb()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "MoneyBack.Web")))
        {
            dir = dir.Parent;
        }
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "src", "MoneyBack.Web");
    }

    private static IEnumerable<string> Razors() =>
        Directory.EnumerateFiles(Raiz, "*.razor", SearchOption.AllDirectories);

    private static string Css() =>
        File.ReadAllText(Path.Combine(Raiz, "wwwroot", "css", "app.css"));

    /// <summary>
    /// Icon.razor tiene un `default` que dibuja un círculo vacío, así que un
    /// nombre mal escrito no rompe nada: solo deja un ícono genérico que
    /// nadie nota hasta verlo en el teléfono.
    /// </summary>
    [Fact]
    public void TodosLosIconosQueSeUsanExisten()
    {
        var icono = File.ReadAllText(Path.Combine(Raiz, "Shared", "Icon.razor"));
        var definidos = Regex.Matches(icono, @"case ""([a-z-]+)"":")
            .Select(m => m.Groups[1].Value).ToHashSet();

        var faltantes = new List<string>();
        foreach (var archivo in Razors())
        {
            foreach (Match uso in Regex.Matches(File.ReadAllText(archivo), @"<Icon\s+Name=""([a-z-]+)"""))
            {
                var nombre = uso.Groups[1].Value;
                if (!definidos.Contains(nombre))
                {
                    faltantes.Add($"{Path.GetFileName(archivo)}: {nombre}");
                }
            }
        }

        Assert.Empty(faltantes);
    }

    /// <summary>
    /// Una var() indefinida no se ignora sola: invalida TODA la declaración.
    /// `border-left: 3px solid var(--color-success)` con --color-success sin
    /// definir no pinta un borde de otro color, no pinta ningún borde — que
    /// es lo que pasaba en Vivienda, donde el verde de "esto sí te cabe en
    /// el ingreso" no se veía y el rojo del "no te cabe" sí.
    /// </summary>
    [Fact]
    public void TodaVariableDeColorQueSeUsaEstaDefinida()
    {
        var css = Css();
        var definidas = Regex.Matches(css, @"^\s*(--[a-z0-9-]+)\s*:", RegexOptions.Multiline)
            .Select(m => m.Groups[1].Value).ToHashSet();

        var usadas = new HashSet<string>();
        foreach (var archivo in Razors().Append(Path.Combine(Raiz, "wwwroot", "css", "app.css")))
        {
            foreach (Match uso in Regex.Matches(File.ReadAllText(archivo), @"var\((--[a-z0-9-]+)"))
            {
                usadas.Add(uso.Groups[1].Value);
            }
        }

        // --tono lo pone cada mosaico en su atributo style, y
        // --blazor-load-percentage la inyecta el runtime de Blazor mientras
        // baja la app: ninguna de las dos puede estar en el CSS.
        usadas.Remove("--tono");
        usadas.Remove("--blazor-load-percentage");

        Assert.Empty(usadas.Except(definidas));
    }

    /// <summary>
    /// El tema oscuro tiene que redefinir lo mismo que define el claro. Una
    /// variable que se olvide queda con el valor claro sobre fondo oscuro:
    /// texto casi negro sobre gris oscuro, ilegible y difícil de notar si
    /// uno trabaja siempre en claro.
    /// </summary>
    [Fact]
    public void ElTemaOscuroRedefineTodosLosColoresDelClaro()
    {
        var css = Css();

        HashSet<string> VariablesDe(int desde)
        {
            var fin = css.IndexOf('}', desde);
            return Regex.Matches(css[desde..fin], @"(--color-[a-z0-9-]+)\s*:")
                .Select(m => m.Groups[1].Value).ToHashSet();
        }

        var claro = VariablesDe(css.IndexOf(":root {", StringComparison.Ordinal));
        var oscuroExplicito = VariablesDe(css.IndexOf(":root[data-theme=\"dark\"]", StringComparison.Ordinal));
        var oscuroDelSistema = VariablesDe(css.IndexOf(":root:not([data-theme=\"light\"])", StringComparison.Ordinal));

        Assert.Empty(claro.Except(oscuroExplicito));
        Assert.Empty(claro.Except(oscuroDelSistema));
    }

    /// <summary>
    /// Nada de datos personales en los ejemplos. Un marcador de posición con
    /// el nombre de quien usa la app se ve como si el dato ya estuviera
    /// guardado, y en las capturas que uno comparte sale su nombre real.
    /// </summary>
    [Theory]
    [InlineData("placeholder")]
    [InlineData("Detalle")]
    public void LosEjemplosNoUsanDatosDeQuienUsaLaApp(string atributo)
    {
        var prohibidos = new[] { "Jesus", "Jesús", "Juliana", "Nuncira" };

        var encontrados = new List<string>();
        foreach (var archivo in Razors())
        {
            var texto = File.ReadAllText(archivo);
            foreach (Match m in Regex.Matches(texto, atributo + @"=""([^""]*)""", RegexOptions.IgnoreCase))
            {
                if (prohibidos.Any(p => m.Groups[1].Value.Contains(p, StringComparison.OrdinalIgnoreCase)))
                {
                    encontrados.Add($"{Path.GetFileName(archivo)}: {m.Value}");
                }
            }
        }

        Assert.Empty(encontrados);
    }

    /// <summary>
    /// Todo lo que no se pueda deshacer pasa por la hoja de confirmación.
    /// Si aparece un DELETE nuevo sin ella, esta prueba lo dice: son cuatro
    /// los que ya se habían escapado.
    /// </summary>
    [Fact]
    public void NingunBorradoOcurreSinPreguntarPrimero()
    {
        var sinPreguntar = new List<string>();

        foreach (var archivo in Razors())
        {
            var texto = File.ReadAllText(archivo);
            if (!Regex.IsMatch(texto, @"Api\.Eliminar\w+Async")) continue;
            if (!texto.Contains("ConfirmacionService"))
            {
                sinPreguntar.Add(Path.GetFileName(archivo));
            }
        }

        Assert.Empty(sinPreguntar);
    }

    /// <summary>
    /// Un spinner centrado deja la pantalla en blanco y hace que el
    /// contenido "salte" cuando llega. Las pantallas con datos usan
    /// esqueleto; el login y la bienvenida no tienen forma que anticipar.
    /// </summary>
    [Fact]
    public void LasPantallasConDatosUsanEsqueletoYNoSpinnerCentrado()
    {
        var conSpinner = Directory.EnumerateFiles(Path.Combine(Raiz, "Pages"), "*.razor")
            .Where(a => File.ReadAllText(a).Contains("center-screen\"><div class=\"spinner"))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(conSpinner);
    }

    /// <summary>
    /// El botón flotante es fijo y tapa lo que quede debajo. Si el relleno
    /// inferior de la página no llega más arriba que el botón, la última
    /// fila de cualquier lista queda atrapada: sus controles —el lápiz y la
    /// X— no se pueden tocar y no hay forma de sacarla scrolleando.
    ///
    /// Pasó de verdad. Esta prueba compara los dos números.
    /// </summary>
    [Fact]
    public void ElRellenoInferiorDejaPasarAlBotonFlotante()
    {
        var css = Css();

        var fab = Regex.Match(css, @"\.fab\s*\{[^}]*?bottom:\s*calc\((\d+)px");
        var alto = Regex.Match(css, @"\.fab\s*\{[^}]*?width:\s*(\d+)px");
        var relleno = Regex.Match(css, @"\.app-shell\s*\{.*?calc\((\d+)px \+ env\(safe-area-inset-bottom\)\)",
            RegexOptions.Singleline);

        Assert.True(fab.Success && alto.Success, "No pude leer la posición del botón flotante.");
        Assert.True(relleno.Success,
            "El relleno inferior de .app-shell debe ser un calc() que incluya env(safe-area-inset-bottom): "
            + "el botón sí lo cuenta, así que si el relleno no, en un iPhone con barra de inicio descuadra.");

        var bordeSuperiorDelBoton = int.Parse(fab.Groups[1].Value) + int.Parse(alto.Groups[1].Value);
        var rellenoInferior = int.Parse(relleno.Groups[1].Value);

        Assert.True(
            rellenoInferior >= bordeSuperiorDelBoton + 12,
            $"El botón llega hasta {bordeSuperiorDelBoton}px y el relleno solo reserva {rellenoInferior}px: "
            + "la última fila queda debajo del botón y no se puede destapar.");
    }

    /// <summary>
    /// El botón se aparta al bajar, y eso lo decide un escucha de scroll en
    /// JS. Si el nombre de la función deja de coincidir, la llamada falla en
    /// silencio (está envuelta en try/catch a propósito) y nadie se entera.
    /// </summary>
    [Fact]
    public void LaFuncionQueVigilaElScrollExisteConEseNombre()
    {
        var js = File.ReadAllText(Path.Combine(Raiz, "wwwroot", "js", "interop.js"));
        var usos = Razors()
            .SelectMany(a => Regex.Matches(File.ReadAllText(a), @"InvokeAsync<[^>]+>\(""(moneyback\.[a-zA-Z]+)"""))
            .Select(m => m.Groups[1].Value)
            .Distinct();

        foreach (var uso in usos)
        {
            var nombre = uso.Split('.')[1];
            Assert.True(js.Contains($"window.moneyback.{nombre}"), $"interop.js no define {uso}.");
        }
    }
}
