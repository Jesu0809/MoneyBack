using System.Net;

namespace MoneyBack.Api.Tests;

/// <summary>
/// La app tiene que seguir en pie aunque la base no responda.
///
/// Antes no lo hacía, y el modo de falla era el peor posible: /health
/// consultaba la base, así que un tropiezo de Neon hacía fallar el chequeo,
/// Fly mataba la máquina, la máquina arrancaba, el chequeo volvía a fallar.
/// Un ciclo del que la app no sale sola ni cuando la base ya volvió. Y
/// encima tres tareas de arranque tocaban la base antes de empezar a
/// atender, así que ni siquiera llegaba a contestar.
/// </summary>
public class ArranqueResistenteTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ArranqueResistenteTests(ApiFactory factory) => _factory = factory;

    /// <summary>
    /// /health es prueba de vida: dice si el proceso está en pie. Es la que
    /// Fly usa para decidir si reinicia, así que NO puede depender de nada
    /// externo.
    /// </summary>
    [Fact]
    public async Task ElChequeoDeVidaRespondeYNoTocaLaBase()
    {
        var respuesta = await _factory.CreateClient().GetAsync("/health");

        respuesta.EnsureSuccessStatusCode();
        Assert.Equal("Healthy", (await respuesta.Content.ReadAsStringAsync()).Trim('"'));
    }

    [Fact]
    public async Task ElChequeoDeLaBaseSigueExistiendoParaDiagnosticar()
    {
        var respuesta = await _factory.CreateClient().GetAsync("/health/base");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    /// <summary>
    /// Las dos rutas son distintas a propósito. Si alguien volviera a
    /// apuntar /health al chequeo con base de datos, se repetiría el ciclo
    /// de reinicios del que la app no sale sola.
    /// </summary>
    [Fact]
    public void FlyApuntaSuChequeoALaRutaDeVidaYNoALaDeLaBase()
    {
        var raiz = UbicarApi();
        var flyToml = File.ReadAllText(Path.Combine(raiz, "fly.toml"));
        var programa = File.ReadAllText(Path.Combine(raiz, "Program.cs"));

        Assert.Contains("path = \"/health\"", flyToml);
        Assert.DoesNotContain("path = \"/health/base\"", flyToml);

        // La ruta que vigila Fly no puede estar atada al chequeo con base.
        Assert.Contains("app.MapGet(\"/health\"", programa);
        Assert.Contains("app.MapHealthChecks(\"/health/base\")", programa);
    }

    /// <summary>
    /// Ninguna tarea de arranque puede quedar sin protección: si lanza, la
    /// app no llega a atender y Fly la reinicia para siempre.
    /// </summary>
    [Fact]
    public void LasTareasDeArranqueNoPuedenTumbarLaApp()
    {
        var programa = File.ReadAllText(Path.Combine(UbicarApi(), "Program.cs"));
        var sinProteger = new List<string>();

        foreach (var linea in programa.Split('\n'))
        {
            var t = linea.Trim();
            // Una tarea de arranque sobre `app` que se espera directo.
            if (t.StartsWith("await app.") && t.EndsWith("Async();"))
            {
                sinProteger.Add(t);
            }
        }

        Assert.Empty(sinProteger);
    }

    private static string UbicarApi()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "MoneyBack.Api")))
        {
            dir = dir.Parent;
        }
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "src", "MoneyBack.Api");
    }
}
