using System.Globalization;
using MoneyBack.Web.Models;
using MoneyBack.Web.Pages;
using MoneyBack.Web.Tests.Infra;

namespace MoneyBack.Web.Tests;

/// <summary>
/// La app corre en el navegador del teléfono, y esos teléfonos están en
/// español de Colombia — así que CurrentCulture en producción es es-CO, no
/// la invariante con la que corren las pruebas por defecto.
/// </summary>
public class CulturaTests
{
    /// <summary>
    /// En un hilo aparte para que no se filtre a las demás pruebas: cambiar
    /// la cultura del hilo actual dentro de un test async la deja pegada en
    /// el hilo del pool y contamina lo que corra después.
    /// </summary>
    private static void EnEsCO(Action prueba)
    {
        Exception? falla = null;
        var hilo = new Thread(() =>
        {
            CultureInfo.CurrentCulture = new CultureInfo("es-CO");
            CultureInfo.CurrentUICulture = new CultureInfo("es-CO");
            try { prueba(); }
            catch (Exception e) { falla = e; }
        });
        hilo.Start();
        hilo.Join();
        if (falla is not null) throw falla;
    }

    [Fact]
    public void RegistrarUnAporteFuncionaConElTelefonoEnEspanol() => EnEsCO(() =>
    {
        using var ctx = new ContextoEnEspanol();
        var meta = Datos.Meta(10);
        ctx.Servidor
            .Responde("GET", "api/metas/10", Datos.Detalle(meta))
            .Responde("POST", "api/metas/10/movimientos", 1, System.Net.HttpStatusCode.Created);

        var pantalla = ctx.RenderComponent<MetaDetalle>(p => p.Add(m => m.MetaId, 10));
        pantalla.Find("button.btn-primary").Click();
        pantalla.Find("input[type=number]").Change("500000");
        pantalla.Find("form").Submit();

        var llamada = ctx.Servidor.Llamadas.LastOrDefault(l => l.Ruta == "api/metas/10/movimientos");
        Assert.NotEqual(default, llamada);
        Assert.Contains("500000", llamada.Cuerpo);
    });

    /// <summary>
    /// El monto del día a día es el campo más usado de la app. Si el
    /// separador decimal de la cultura lo rompe, no se puede registrar nada.
    /// </summary>
    [Fact]
    public void RegistrarUnGastoFuncionaConElTelefonoEnEspanol() => EnEsCO(() =>
    {
        using var ctx = new ContextoEnEspanol();
        ctx.Servidor
            .Responde("GET", "api/movimientos-diaadia/resumen", new ResumenDiaADiaResponse(0, 0, 0, []))
            .Responde("GET", "api/movimientos-diaadia", Array.Empty<object>())
            .Responde("GET", "api/categorias", new[] { Datos.Categoria(100, "Mercado") })
            .Responde("GET", "api/tarjetas-credito", Array.Empty<object>())
            .Responde("GET", "api/metas/destino-aporte", new DestinoAporteResponse("SinMetas", null, null, null, 0))
            .Responde("POST", "api/movimientos-diaadia",
                new MovimientoDiaADiaResponse(1, 100, "Mercado", "🛒", TipoCategoria.Gasto,
                    47_300, DateTime.UtcNow, null, null, false, null, null),
                System.Net.HttpStatusCode.Created);

        var pantalla = ctx.RenderComponent<DiaADia>();
        pantalla.Find("button.fab").Click();
        pantalla.Find(".amount-entry input").Change("47300");
        pantalla.Find(".category-tile").Click();
        pantalla.Find("form").Submit();

        var llamada = ctx.Servidor.Llamadas.LastOrDefault(l => l is { Metodo: "POST", Ruta: "api/movimientos-diaadia" });
        Assert.NotEqual(default, llamada);
        Assert.Contains("47300", llamada.Cuerpo);
    });

    /// <summary>
    /// Los pesos se formatean a mano justo para no depender de ICU, que en
    /// WASM puede venir recortado. Tiene que dar lo mismo en cualquier
    /// cultura.
    /// </summary>
    [Fact]
    public void ElFormatoDePesosNoDependeDeLaCultura()
    {
        var invariante = MoneyBack.Web.Services.Formato.Pesos(1_234_567);
        string? enEspanol = null;
        EnEsCO(() => enEspanol = MoneyBack.Web.Services.Formato.Pesos(1_234_567));

        Assert.Equal("$1.234.567", invariante);
        Assert.Equal(invariante, enEspanol);
    }

    /// <summary>Expone el servidor falso, que en la base es protegido.</summary>
    private sealed class ContextoEnEspanol : PruebaDePantalla
    {
        public new ServidorFalso Servidor => base.Servidor;
    }
}
