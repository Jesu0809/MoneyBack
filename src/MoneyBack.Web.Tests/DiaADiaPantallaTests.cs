using System.Net;
using MoneyBack.Web.Models;
using MoneyBack.Web.Pages;
using MoneyBack.Web.Tests.Infra;

namespace MoneyBack.Web.Tests;

/// <summary>
/// La pantalla que más se usa. Todo lo que se rompa acá se rompe todos los
/// días.
/// </summary>
public class DiaADiaPantallaTests : PruebaDePantalla
{
    private static ResumenDiaADiaResponse Resumen(decimal ingresos = 3_000_000, decimal gastos = 1_200_000,
        params TotalPorCategoria[] porCategoria) =>
        new(ingresos, gastos, ingresos - gastos, porCategoria.ToList());

    private static MovimientoDiaADiaResponse Movimiento(
        int id = 1, string categoria = "Mercado", string icono = "🛒",
        TipoCategoria tipo = TipoCategoria.Gasto, decimal monto = 47_300,
        string? nota = null, string? comercio = null) =>
        new(id, 100, categoria, icono, tipo, monto, DateTime.UtcNow, nota, comercio, false, null, null);

    /// <summary>El andamiaje mínimo: todo vacío y todo respondiendo.</summary>
    private void ServidorBase(
        object? movimientos = null,
        object? destino = null,
        object? categorias = null,
        object? tarjetas = null)
    {
        Servidor
            .Responde("GET", "api/movimientos-diaadia/resumen", Resumen())
            .Responde("GET", "api/movimientos-diaadia", movimientos ?? Array.Empty<object>())
            .Responde("GET", "api/categorias", categorias ?? Array.Empty<object>())
            .Responde("GET", "api/tarjetas-credito", tarjetas ?? Array.Empty<object>())
            .Responde("GET", "api/metas/destino-aporte",
                destino ?? new DestinoAporteResponse("SinMetas", null, null, null, 0));
    }

    [Fact]
    public void MuestraElSaldoAcumuladoEnPesosColombianos()
    {
        ServidorBase();

        var pantalla = RenderComponent<DiaADia>();

        Assert.Contains("Saldo acumulado", pantalla.Markup);
        Assert.Contains("1.800.000", pantalla.Markup);
    }

    // --- El botón de aporte: los cuatro casos que definen a dónde lleva ---

    [Fact]
    public void SinMetas_ElBotonInvitaACrearLaPrimeraYLlevaAGrupos()
    {
        ServidorBase(destino: new DestinoAporteResponse("SinMetas", null, null, null, 0));

        var pantalla = RenderComponent<DiaADia>();
        Assert.Contains("Crear nuestra primera meta", pantalla.Markup);

        pantalla.Find("button.btn-secondary.btn-block").Click();
        Assert.Equal("hogar", RutaActual);
    }

    [Fact]
    public void ConUnaSolaMeta_ElBotonLaNombraYVaDirecto()
    {
        ServidorBase(destino: new DestinoAporteResponse("Unica", 7, "Cuota inicial", "🏠", 1));

        var pantalla = RenderComponent<DiaADia>();
        Assert.Contains("Aportar a 🏠 Cuota inicial", pantalla.Markup);

        pantalla.Find("button.btn-secondary.btn-block").Click();
        Assert.Equal("metas/7", RutaActual);
    }

    [Fact]
    public void ConFavorita_VaDirectoAEsaAunqueHayaVarias()
    {
        ServidorBase(destino: new DestinoAporteResponse("Favorita", 9, "Viaje", "✈️", 4));

        var pantalla = RenderComponent<DiaADia>();
        Assert.Contains("Aportar a ✈️ Viaje", pantalla.Markup);

        pantalla.Find("button.btn-secondary.btn-block").Click();
        Assert.Equal("metas/9", RutaActual);
    }

    [Fact]
    public void ConVariasYSinFavorita_MandaAElegir()
    {
        ServidorBase(destino: new DestinoAporteResponse("Varias", null, null, null, 3));

        var pantalla = RenderComponent<DiaADia>();
        Assert.Contains("Aportar a una meta", pantalla.Markup);

        pantalla.Find("button.btn-secondary.btn-block").Click();
        Assert.Equal("elegir-meta", RutaActual);
    }

    // --- Sin clasificar: el corazón del registro automático ---

    [Fact]
    public void LosGastosSinClasificarSeResaltanAparte()
    {
        ServidorBase(movimientos: new[]
        {
            Movimiento(1, "Sin clasificar", "❓", comercio: "EXITO CHAPINERO"),
            Movimiento(2, "Mercado")
        });

        var pantalla = RenderComponent<DiaADia>();

        Assert.Contains("1 gasto sin clasificar", pantalla.Markup);
        Assert.Contains("EXITO CHAPINERO", pantalla.Markup);
    }

    [Fact]
    public void SinComercioNiNota_DiceQueElBancoNoInformo()
    {
        ServidorBase(movimientos: new[] { Movimiento(1, "Sin clasificar", "❓") });

        var pantalla = RenderComponent<DiaADia>();

        Assert.Contains("El banco no dijo dónde", pantalla.Markup);
    }

    // --- El formulario de registro ---

    [Fact]
    public void ElFormularioAbreConLasCategoriasDeGasto()
    {
        ServidorBase(categorias: new[]
        {
            Datos.Categoria(100, "Mercado", "🛒"),
            Datos.Categoria(101, "Sueldo", "💼", TipoCategoria.Ingreso)
        });

        var pantalla = RenderComponent<DiaADia>();
        pantalla.Find("button.fab").Click();

        var tiles = pantalla.FindAll(".category-tile");
        Assert.Single(tiles);
        Assert.Contains("Mercado", tiles[0].TextContent);
    }

    /// <summary>
    /// Cambiar a Ingreso tiene que cambiar las categorías: si quedaran las de
    /// gasto, registrar un sueldo sería imposible sin darse cuenta de por qué.
    /// </summary>
    [Fact]
    public void CambiarAIngreso_CambiaLasCategoriasOfrecidas()
    {
        ServidorBase(categorias: new[]
        {
            Datos.Categoria(100, "Mercado", "🛒"),
            Datos.Categoria(101, "Sueldo", "💼", TipoCategoria.Ingreso)
        });

        var pantalla = RenderComponent<DiaADia>();
        pantalla.Find("button.fab").Click();
        pantalla.FindAll(".segmented-btn").First(b => b.TextContent == "Ingreso").Click();

        var tiles = pantalla.FindAll(".category-tile");
        Assert.Single(tiles);
        Assert.Contains("Sueldo", tiles[0].TextContent);
    }

    /// <summary>
    /// Sin categorías del tipo elegido, el botón de guardar tiene que estar
    /// bloqueado: si no, se manda una petición con CategoriaId = 0 y el
    /// servidor responde un error que no explica nada.
    /// </summary>
    [Fact]
    public void SinCategoriasDelTipoElegido_NoDejaGuardarYExplicaPorQue()
    {
        ServidorBase(categorias: Array.Empty<object>());

        var pantalla = RenderComponent<DiaADia>();
        pantalla.Find("button.fab").Click();

        Assert.Contains("Todavía no tienes categorías de gasto", pantalla.Markup);
        Assert.True(pantalla.Find(".fullscreen-footer button").HasAttribute("disabled"));
    }

    /// <summary>
    /// El selector de tarjeta solo aparece si hay alguna: mostrar un combo
    /// con una sola opción ("efectivo") es ruido en la pantalla que más se usa.
    /// </summary>
    [Fact]
    public void SinTarjetas_NoApareceElSelectorDePagadoCon()
    {
        ServidorBase(categorias: new[] { Datos.Categoria() });

        var pantalla = RenderComponent<DiaADia>();
        pantalla.Find("button.fab").Click();

        Assert.DoesNotContain("Pagado con", pantalla.Markup);
    }

    [Fact]
    public void ConTarjetas_ApareceElSelectorConLaOpcionDeEfectivo()
    {
        ServidorBase(
            categorias: new[] { Datos.Categoria() },
            tarjetas: new[] { new TarjetaCreditoResponse(3, "Visa Davivienda", 15, true) });

        var pantalla = RenderComponent<DiaADia>();
        pantalla.Find("button.fab").Click();

        Assert.Contains("Pagado con", pantalla.Markup);
        Assert.Contains("Efectivo / débito", pantalla.Markup);
        Assert.Contains("Visa Davivienda", pantalla.Markup);
    }

    /// <summary>
    /// Una tarjeta archivada no debería ofrecerse: seguiría sumando saldo
    /// pendiente a algo que ya no se usa.
    /// </summary>
    [Fact]
    public void UnaTarjetaArchivadaNoSeOfrece()
    {
        ServidorBase(
            categorias: new[] { Datos.Categoria() },
            tarjetas: new[]
            {
                new TarjetaCreditoResponse(3, "Visa activa", 15, true),
                new TarjetaCreditoResponse(4, "Vieja cerrada", 20, false)
            });

        var pantalla = RenderComponent<DiaADia>();
        pantalla.Find("button.fab").Click();

        Assert.Contains("Visa activa", pantalla.Markup);
        Assert.DoesNotContain("Vieja cerrada", pantalla.Markup);
    }

    [Fact]
    public void RegistrarUnGasto_MandaMontoYCategoria()
    {
        ServidorBase(categorias: new[] { Datos.Categoria(100, "Mercado") });
        Servidor.Responde("POST", "api/movimientos-diaadia",
            Movimiento(), HttpStatusCode.Created);

        var pantalla = RenderComponent<DiaADia>();
        pantalla.Find("button.fab").Click();
        pantalla.Find(".amount-entry input").Change("47300");
        pantalla.Find(".category-tile").Click();
        pantalla.Find("form").Submit();

        var llamada = Servidor.Llamadas.Last(l => l is { Metodo: "POST", Ruta: "api/movimientos-diaadia" });
        Assert.Contains("\"categoriaId\":100", llamada.Cuerpo);
        Assert.Contains("47300", llamada.Cuerpo);
    }

    // --- Rendimiento: cuántas veces habla con el servidor al abrir ---

    /// <summary>
    /// Cada petición desde Colombia cuesta ~0,3 s (casi todo TLS y distancia,
    /// no el servidor). Esta prueba fija el presupuesto de llamadas de la
    /// pantalla principal: si alguien agrega una consulta al arranque, tiene
    /// que ser una decisión consciente y no un descuido.
    /// </summary>
    [Fact]
    public void AlAbrir_NoHablaConElServidorMasDeLoNecesario()
    {
        ServidorBase();

        RenderComponent<DiaADia>();

        var rutas = Servidor.Llamadas.Select(l => l.Ruta).ToList();
        Assert.True(rutas.Count <= 6, $"La pantalla hizo {rutas.Count} peticiones: {string.Join(", ", rutas)}");
    }
}
