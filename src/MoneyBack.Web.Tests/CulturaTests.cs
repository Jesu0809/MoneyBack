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
    private static void EnEsCO(Action prueba) => Cultura.ComoEnColombia(prueba);

    [Fact]
    public void RegistrarUnAporteFuncionaConElTelefonoEnEspanol() => EnEsCO(() =>
    {
        using var ctx = new ContextoEnEspanol();
        var meta = Datos.Meta(10);
        ctx.Servidor
            .Responde("GET", "api/metas/10", Datos.Detalle(meta))
            .Responde("POST", "api/metas/10/movimientos", 1, System.Net.HttpStatusCode.Created);

        var pantalla = ctx.RenderComponent<MetaDetalle>(p => p.Add(m => m.MetaId, 10));

        // Esperar a que la meta haya cargado antes de tocar nada: RenderComponent
        // vuelve apenas se pinta el esqueleto, y con la máquina ocupada el clic
        // llegaba antes que el formulario. Fallaba una de cada tantas y parecía
        // un problema de cultura.
        pantalla.WaitForAssertion(() => Assert.NotEmpty(pantalla.FindAll("button.btn-primary")));
        pantalla.Find("button.btn-primary").Click();
        pantalla.WaitForAssertion(() => Assert.NotEmpty(pantalla.FindAll("input[type=number]")));
        pantalla.Find("input[type=number]").Change("500000");
        pantalla.Find("form").Submit();

        pantalla.WaitForAssertion(() => Assert.Contains(
            ctx.Servidor.Llamadas, l => l.Ruta == "api/metas/10/movimientos"));

        var llamada = ctx.Servidor.Llamadas.Last(l => l.Ruta == "api/metas/10/movimientos");
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

        // Las categorías llegan en una segunda tanda de peticiones; sin
        // esperarlas, la cuadrícula está vacía y no hay nada que tocar.
        pantalla.WaitForAssertion(() => Assert.NotEmpty(pantalla.FindAll(".category-tile")));
        pantalla.Find(".amount-entry input").Change("47300");
        pantalla.Find(".category-tile").Click();
        pantalla.Find("form").Submit();

        pantalla.WaitForAssertion(() => Assert.Contains(
            ctx.Servidor.Llamadas, l => l is { Metodo: "POST", Ruta: "api/movimientos-diaadia" }));

        var llamada = ctx.Servidor.Llamadas.Last(l => l is { Metodo: "POST", Ruta: "api/movimientos-diaadia" });
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
