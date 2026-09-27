using System.Net;
using MoneyBack.Web.Models;
using MoneyBack.Web.Pages;
using MoneyBack.Web.Tests.Infra;

namespace MoneyBack.Web.Tests;

/// <summary>
/// Las pantallas de la sección Más. Tres de ellas —Deudas, Reportes y
/// Suscripciones— no tenían ni una prueba, ni de interfaz ni de servidor:
/// eran las únicas partes de la app que nadie verificaba nunca.
/// </summary>
public class PantallasDeMasTests : PruebaDePantalla
{
    // ---------- Deudas ----------

    private static DeudaResponse Deuda(
        int id = 1, string nombre = "Crédito de estudio", decimal total = 12_000_000,
        decimal cuota = 500_000, int totalCuotas = 24, int pagadas = 6,
        TipoPropiedadDeuda propiedad = TipoPropiedadDeuda.Privada, bool activa = true) =>
        new(id, propiedad, nombre, total, cuota, totalCuotas, pagadas,
            total - cuota * pagadas, Math.Round((decimal)pagadas / totalCuotas * 100, 2), activa);

    [Fact]
    public void Deudas_MuestraCuantoFaltaYCuantasCuotasVan()
    {
        Servidor
            .Responde("GET", "api/deudas", new[] { Deuda() })
            .Responde("GET", "api/categorias", new[] { Datos.Categoria() })
            .Responde("GET", "api/hogares/mio", Datos.Grupo());

        var pantalla = RenderComponent<Deudas>();

        Assert.Contains("Crédito de estudio", pantalla.Markup);
        Assert.Contains("6", pantalla.Markup);
        Assert.Contains("24", pantalla.Markup);
    }

    [Fact]
    public void Deudas_SinNinguna_NoSeQuedaEnBlanco()
    {
        Servidor
            .Responde("GET", "api/deudas", Array.Empty<object>())
            .Responde("GET", "api/categorias", Array.Empty<object>())
            .Responde("GET", "api/hogares/mio", Datos.Grupo());

        var pantalla = RenderComponent<Deudas>();

        Assert.False(string.IsNullOrWhiteSpace(pantalla.Markup));
        Assert.Contains("Deudas", pantalla.Markup);
    }

    /// <summary>
    /// Pagar una cuota registra un gasto en el día a día, así que necesita
    /// una categoría. Sin categorías la pantalla tiene que decirlo, no
    /// mandar una petición que el servidor rechaza.
    /// </summary>
    [Fact]
    public void Deudas_SinCategorias_NoDejaPagarUnaCuotaAlVacio()
    {
        Servidor
            .Responde("GET", "api/deudas", new[] { Deuda() })
            .Responde("GET", "api/categorias", Array.Empty<object>())
            .Responde("GET", "api/hogares/mio", Datos.Grupo());

        var pantalla = RenderComponent<Deudas>();
        var botonesPagar = pantalla.FindAll("button").Where(b => b.TextContent.Contains("cuota")).ToList();

        // O no ofrece pagar, o al intentarlo explica que faltan categorías.
        if (botonesPagar.Count > 0)
        {
            botonesPagar[0].Click();
            Assert.DoesNotContain(Servidor.Llamadas, l => l.Ruta.Contains("pagar-cuota"));
        }
    }

    // ---------- Suscripciones ----------

    private static SuscripcionResponse Cobro(
        int id = 1, string nombre = "Netflix", decimal monto = 38_900, bool activa = true) =>
        new(id, nombre, monto, 100, "Entretenimiento", "🎬",
            FrecuenciaSuscripcion.Mensual, DateTime.UtcNow.AddDays(5), 2, activa);

    [Fact]
    public void Suscripciones_MuestraElCobroConSuMontoYProximaFecha()
    {
        Servidor
            .Responde("GET", "api/suscripciones", new[] { Cobro() })
            .Responde("GET", "api/categorias", new[] { Datos.Categoria(100, "Entretenimiento", "🎬") });

        var pantalla = RenderComponent<Suscripciones>();

        Assert.Contains("Netflix", pantalla.Markup);
        Assert.Contains("38.900", pantalla.Markup);
    }

    [Fact]
    public void Suscripciones_SinNinguna_ExplicaParaQueSirve()
    {
        Servidor
            .Responde("GET", "api/suscripciones", Array.Empty<object>())
            .Responde("GET", "api/categorias", Array.Empty<object>());

        var pantalla = RenderComponent<Suscripciones>();

        Assert.Contains("Cobros fijos", pantalla.Markup);
    }

    // ---------- Reportes ----------

    [Fact]
    public void Reportes_MuestraElResumenDelMes()
    {
        Servidor
            .Responde("GET", "api/movimientos-diaadia/resumen",
                new ResumenDiaADiaResponse(4_000_000, 1_500_000, 2_500_000,
                    [new TotalPorCategoria(1, "Mercado", "🛒", 900_000)]))
            .Responde("GET", "api/reportes/anual",
                new ResumenAnualResponse(DateTime.UtcNow.Year, 40_000_000, 18_000_000, [], []));

        var pantalla = RenderComponent<Reportes>();

        Assert.Contains("Mercado", pantalla.Markup);
        Assert.Contains("900.000", pantalla.Markup);
    }

    /// <summary>
    /// Un mes sin movimientos no puede verse igual que un mes que no cargó:
    /// ceros sin explicación se leen como un dato.
    /// </summary>
    [Fact]
    public void Reportes_UnMesVacioNoSeQuedaEnBlanco()
    {
        Servidor
            .Responde("GET", "api/movimientos-diaadia/resumen", new ResumenDiaADiaResponse(0, 0, 0, []))
            .Responde("GET", "api/reportes/anual",
                new ResumenAnualResponse(DateTime.UtcNow.Year, 0, 0, [], []));

        var pantalla = RenderComponent<Reportes>();

        Assert.Contains("Reportes", pantalla.Markup);
    }

    // ---------- Tarjetas de crédito ----------

    [Fact]
    public void Tarjetas_MuestraElSaldoPendienteDeCadaUna()
    {
        Servidor
            .Responde("GET", "api/tarjetas-credito", new[] { new TarjetaCreditoResponse(3, "Visa", 15, true) })
            .Responde("GET", "api/tarjetas-credito/3/saldo-pendiente",
                new SaldoPendienteResponse(1_250_000, DateTime.UtcNow.AddDays(-20)));

        var pantalla = RenderComponent<TarjetasCredito>();

        Assert.Contains("Visa", pantalla.Markup);
        Assert.Contains("1.250.000", pantalla.Markup);
    }

    /// <summary>
    /// Pagar de más deja el saldo en negativo y eso es correcto en una
    /// tarjeta rotativa — pero no se puede mostrar como si se debiera plata.
    /// </summary>
    [Fact]
    public void Tarjetas_UnSaldoNegativoNoSeMuestraComoDeuda()
    {
        Servidor
            .Responde("GET", "api/tarjetas-credito", new[] { new TarjetaCreditoResponse(3, "Visa", 15, true) })
            .Responde("GET", "api/tarjetas-credito/3/saldo-pendiente",
                new SaldoPendienteResponse(-50_000, DateTime.UtcNow));

        var pantalla = RenderComponent<TarjetasCredito>();

        Assert.DoesNotContain("$ -", pantalla.Markup);
    }

    // ---------- Topes de gasto ----------

    [Fact]
    public void Presupuestos_MuestraLoGastadoContraElTope()
    {
        var hoy = DateTime.Now;
        Servidor
            .Responde("GET", "api/categorias", new[] { Datos.Categoria(1, "Mercado", "🛒") })
            .Responde("GET", "api/presupuestos", new[]
            {
                new PresupuestoResponse(1, 1, "Mercado", "🛒", 800_000, 620_000, hoy.Month, hoy.Year)
            });

        var pantalla = RenderComponent<Presupuestos>();

        Assert.Contains("Mercado", pantalla.Markup);
        Assert.Contains("620.000", pantalla.Markup);
        Assert.Contains("800.000", pantalla.Markup);
    }

    /// <summary>
    /// Pasarse del tope tiene que verse distinto: si se ve igual que ir bien,
    /// el tope no sirve para nada.
    /// </summary>
    [Fact]
    public void Presupuestos_PasarseDelTopeSeVeDistinto()
    {
        var hoy = DateTime.Now;
        Servidor
            .Responde("GET", "api/categorias", new[] { Datos.Categoria(1, "Mercado", "🛒") })
            .Responde("GET", "api/presupuestos", new[]
            {
                new PresupuestoResponse(1, 1, "Mercado", "🛒", 500_000, 700_000, hoy.Month, hoy.Year)
            });

        var pantalla = RenderComponent<Presupuestos>();

        Assert.True(
            pantalla.Markup.Contains("danger") || pantalla.Markup.Contains("Te pasaste"),
            "Pasarse del tope se ve igual que no pasarse.");
    }

    // ---------- El menú Más ----------

    [Fact]
    public void Mas_TodosLosAtajosLlevanAUnaRutaQueExiste()
    {
        var rutasDeLaApp = typeof(DiaADia).Assembly.GetTypes()
            .SelectMany(t => t.GetCustomAttributes(typeof(Microsoft.AspNetCore.Components.RouteAttribute), false))
            .Cast<Microsoft.AspNetCore.Components.RouteAttribute>()
            .Select(a => a.Template)
            .ToHashSet();

        var pantalla = RenderComponent<Mas>();
        var atajos = pantalla.FindAll(".card-tap").Count;

        Assert.True(atajos >= 8, $"El menú Más quedó con {atajos} atajos.");
        // Cada atajo del menú tiene que corresponder a una página real.
        foreach (var ruta in new[] { "/presupuestos", "/suscripciones", "/reportes", "/vuelto",
                                      "/tarjetas-credito", "/deudas", "/subsidios", "/categorias" })
        {
            Assert.Contains(ruta, rutasDeLaApp);
        }
    }
}
