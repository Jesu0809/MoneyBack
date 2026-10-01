using MoneyBack.Web.Models;
using MoneyBack.Web.Pages;
using MoneyBack.Web.Services;
using MoneyBack.Web.Shared;
using MoneyBack.Web.Tests.Infra;

namespace MoneyBack.Web.Tests;

/// <summary>
/// CSS solo entiende el punto decimal. El teléfono de quien usa esto está
/// en español, donde el separador es la coma: "163,8deg" y "45,5%" no son
/// valores feos, son INVÁLIDOS, y el navegador descarta la declaración
/// entera.
///
/// Pasó tres veces y las tres de forma invisible desde un escritorio en
/// inglés. La torta de gastos no se pintaba con seis categorías pero sí con
/// una —con una, los grados salen redondos— y eso es justo lo que lo hacía
/// tan difícil de ver.
/// </summary>
public class NumerosEnCssTests : PruebaDePantalla
{
    [Fact]
    public void ElAyudanteUsaPuntoAunqueElIdiomaDigaOtraCosa() => Cultura.ComoEnColombia(() =>
    {
        Assert.Equal("163.8", Css.Numero(163.8m));
        Assert.Equal("45.5", Css.Numero(45.5));
        Assert.Equal("360", Css.Numero(360m));
    });

    // ---------- La torta ----------

    private void ConGastos(params (string Nombre, decimal Total)[] categorias)
    {
        var total = categorias.Sum(c => c.Total);
        var porCategoria = categorias
            .Select((c, i) => new TotalPorCategoria(i + 1, c.Nombre, "🛒", c.Total))
            .ToList();

        Servidor
            .Responde("GET", "api/movimientos-diaadia/resumen",
                new ResumenDiaADiaResponse(0, total, -total, porCategoria))
            .Responde("GET", "api/movimientos-diaadia", Array.Empty<object>())
            .Responde("GET", "api/categorias", Array.Empty<object>())
            .Responde("GET", "api/tarjetas-credito", Array.Empty<object>())
            .Responde("GET", "api/metas/destino-aporte",
                new DestinoAporteResponse("SinMetas", null, null, null, 0));
    }

    /// <summary>
    /// El caso exacto de la pantalla: seis categorías con porcentajes
    /// quebrados. Antes salía "163,8deg" y la torta desaparecía.
    /// </summary>
    [Fact]
    public void LaTortaSePintaConVariasCategoriasEnUnTelefonoEnEspanol() => Cultura.ComoEnColombia(() =>
    {
        ConGastos(
            ("Pago de tarjetas", 300_000m), ("Vivienda", 200_000m), ("Comida", 73_000m),
            ("Entretenimiento", 44_900m), ("Transferencia", 40_000m), ("Otros gastos", 2_000m));

        var pantalla = RenderComponent<DiaADia>();
        var estilo = pantalla.Find(".donut").GetAttribute("style")!;

        Assert.Contains("conic-gradient", estilo);
        Assert.DoesNotContain(",", estilo.Replace("conic-gradient(", "").Split("deg")[0]);
        Assert.Matches(@"\d+\.\d+deg", estilo);
    });

    /// <summary>
    /// Con una sola categoría funcionaba igual, porque 0 y 360 no llevan
    /// decimales. Se deja fijo para que quede claro que ese caso nunca fue
    /// la prueba de que estuviera bien.
    /// </summary>
    [Fact]
    public void ConUnaSolaCategoriaTambien() => Cultura.ComoEnColombia(() =>
    {
        ConGastos(("Transferencia", 20_000m));

        var pantalla = RenderComponent<DiaADia>();

        Assert.Contains("0deg 360deg", pantalla.Find(".donut").GetAttribute("style"));
    });

    [Fact]
    public void SinGastosNoIntentaPintarUnaTorta() => Cultura.ComoEnColombia(() =>
    {
        Servidor
            .Responde("GET", "api/movimientos-diaadia/resumen", new ResumenDiaADiaResponse(0, 0, 0, []))
            .Responde("GET", "api/movimientos-diaadia", Array.Empty<object>())
            .Responde("GET", "api/categorias", Array.Empty<object>())
            .Responde("GET", "api/tarjetas-credito", Array.Empty<object>())
            .Responde("GET", "api/metas/destino-aporte",
                new DestinoAporteResponse("SinMetas", null, null, null, 0));

        var pantalla = RenderComponent<DiaADia>();

        Assert.Empty(pantalla.FindAll(".donut"));
    });

    // ---------- La barra de progreso de las metas ----------

    [Fact]
    public void LaBarraDeProgresoSeLlenaEnUnTelefonoEnEspanol() => Cultura.ComoEnColombia(() =>
    {
        using var ctx = new TestContext();
        var barra = ctx.RenderComponent<ProgressBar>(p => p.Add(b => b.Porcentaje, 45.5m));

        barra.WaitForAssertion(() =>
        {
            var estilo = barra.Find(".progress-fill").GetAttribute("style")!;
            Assert.Contains("45.5%", estilo);
            Assert.DoesNotContain(",", estilo);
        });
    });

    [Fact]
    public void LaBarraNoSePasaDelCienAunqueSePasenDeLaMeta() => Cultura.ComoEnColombia(() =>
    {
        using var ctx = new TestContext();
        var barra = ctx.RenderComponent<ProgressBar>(p => p.Add(b => b.Porcentaje, 150m));

        barra.WaitForAssertion(() => Assert.Contains("100%", barra.Find(".progress-fill").GetAttribute("style")));
    });

    /// <summary>
    /// El linter: ningún número puede entrar a un atributo style sin pasar
    /// por Css.Numero. Es el error que ya se repitió tres veces.
    /// </summary>
    [Fact]
    public void NingunNumeroEntraACssSinPasarPorElAyudante()
    {
        var raiz = UbicarWeb();
        var sospechosos = new List<string>();

        foreach (var archivo in Directory.EnumerateFiles(raiz, "*.razor", SearchOption.AllDirectories))
        {
            foreach (var linea in File.ReadAllLines(archivo))
            {
                if (!linea.Contains("style=\"")) continue;
                // El formato ":0.##" dentro de una interpolación usa la
                // cultura actual; en CSS eso es una coma.
                if (!System.Text.RegularExpressions.Regex.IsMatch(linea, @":0\.#|:N\d|:F\d")) continue;
                if (linea.Contains("Css.Numero")) continue;

                sospechosos.Add($"{Path.GetFileName(archivo)}: {linea.Trim()}");
            }
        }

        Assert.Empty(sospechosos);
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
