using System.Net;
using MoneyBack.Web.Models;
using MoneyBack.Web.Pages;
using MoneyBack.Web.Services;
using MoneyBack.Web.Tests.Infra;

namespace MoneyBack.Web.Tests;

/// <summary>
/// Lo que no se puede deshacer se pregunta antes. Había cuatro botones que
/// borraban al primer toque; en el día a día ese botón mide 30 píxeles y
/// está pegado al de editar.
/// </summary>
public class ConfirmacionTests : PruebaDePantalla
{
    // ---------- El servicio ----------

    [Fact]
    public async Task ResponderQueSi_DevuelveVerdadero()
    {
        var pregunta = Confirmacion.PreguntarAsync(new Confirmacion("¿Seguro?"));
        Confirmacion.Responder(true);

        Assert.True(await pregunta);
        Assert.Null(Confirmacion.Pendiente);
    }

    [Fact]
    public async Task ResponderQueNo_DevuelveFalso()
    {
        var pregunta = Confirmacion.PreguntarAsync(new Confirmacion("¿Seguro?"));
        Confirmacion.Responder(false);

        Assert.False(await pregunta);
    }

    /// <summary>
    /// Si una segunda pregunta pisa a la primera, la primera no puede
    /// quedarse esperando para siempre: quien la hizo nunca continuaría.
    /// </summary>
    [Fact]
    public async Task UnaSegundaPregunta_CancelaLaPrimeraEnVezDeColgarla()
    {
        var primera = Confirmacion.PreguntarAsync(new Confirmacion("Primera"));
        var segunda = Confirmacion.PreguntarAsync(new Confirmacion("Segunda"));

        Assert.False(await primera);

        Confirmacion.Responder(true);
        Assert.True(await segunda);
    }

    // ---------- Los cuatro botones que borraban sin preguntar ----------

    [Fact]
    public void DiaADia_BorrarUnGasto_PreguntaYNombraElMonto()
    {
        Servidor
            .Responde("GET", "api/movimientos-diaadia/resumen", new ResumenDiaADiaResponse(0, 47_300, -47_300, []))
            .Responde("GET", "api/movimientos-diaadia", new[]
            {
                new MovimientoDiaADiaResponse(1, 100, "Mercado", "🛒", TipoCategoria.Gasto,
                    47_300, DateTime.UtcNow, null, null, false, null, null)
            })
            .Responde("GET", "api/categorias", Array.Empty<object>())
            .Responde("GET", "api/tarjetas-credito", Array.Empty<object>())
            .Responde("GET", "api/metas/destino-aporte", new DestinoAporteResponse("SinMetas", null, null, null, 0));

        var pantalla = RenderComponent<DiaADia>();
        pantalla.Find("button[title='Eliminar']").Click();

        Assert.NotNull(Confirmacion.Pendiente);
        Assert.Contains("47.300", Confirmacion.Pendiente!.Titulo);
        Assert.Contains("Mercado", Confirmacion.Pendiente.Detalle);
        Assert.DoesNotContain(Servidor.Llamadas, l => l.Metodo == "DELETE");
    }

    [Fact]
    public void DiaADia_SiSeCancela_ElGastoNoSeBorra()
    {
        Servidor
            .Responde("GET", "api/movimientos-diaadia/resumen", new ResumenDiaADiaResponse(0, 47_300, -47_300, []))
            .Responde("GET", "api/movimientos-diaadia", new[]
            {
                new MovimientoDiaADiaResponse(1, 100, "Mercado", "🛒", TipoCategoria.Gasto,
                    47_300, DateTime.UtcNow, null, null, false, null, null)
            })
            .Responde("GET", "api/categorias", Array.Empty<object>())
            .Responde("GET", "api/tarjetas-credito", Array.Empty<object>())
            .Responde("GET", "api/metas/destino-aporte", new DestinoAporteResponse("SinMetas", null, null, null, 0));

        var pantalla = RenderComponent<DiaADia>();
        pantalla.Find("button[title='Eliminar']").Click();
        Confirmacion.Responder(false);

        Assert.DoesNotContain(Servidor.Llamadas, l => l.Metodo == "DELETE");
    }

    [Fact]
    public void CobrosFijos_QuitarUnoPreguntaPrimero()
    {
        Servidor
            .Responde("GET", "api/suscripciones", new[]
            {
                new SuscripcionResponse(1, "Netflix", 38_900, 100, "Entretenimiento", "🎬",
                    FrecuenciaSuscripcion.Mensual, DateTime.UtcNow.AddDays(5), 2, true)
            })
            .Responde("GET", "api/categorias", Array.Empty<object>());

        var pantalla = RenderComponent<Suscripciones>();
        pantalla.FindAll(".card button.icon-btn").Last().Click();

        Assert.NotNull(Confirmacion.Pendiente);
        Assert.Contains("Netflix", Confirmacion.Pendiente!.Titulo);
        Assert.DoesNotContain(Servidor.Llamadas, l => l.Metodo == "DELETE");
    }

    [Fact]
    public void Tarjetas_BorrarUnaPreguntaPrimero()
    {
        Servidor
            .Responde("GET", "api/tarjetas-credito", new[] { new TarjetaCreditoResponse(3, "Visa", 15, true) })
            .Responde("GET", "api/tarjetas-credito/3/saldo-pendiente", new SaldoPendienteResponse(0, null));

        var pantalla = RenderComponent<TarjetasCredito>();
        pantalla.FindAll("button.icon-btn").Last().Click();

        Assert.NotNull(Confirmacion.Pendiente);
        Assert.Contains("Visa", Confirmacion.Pendiente!.Titulo);
        Assert.DoesNotContain(Servidor.Llamadas, l => l.Metodo == "DELETE");
    }

    [Fact]
    public void Deudas_BorrarUnaAvisaQueSePierdenLasCuotasPagadas()
    {
        Servidor
            .Responde("GET", "api/deudas", new[]
            {
                new DeudaResponse(1, TipoPropiedadDeuda.Privada, "Crédito de estudio",
                    12_000_000, 500_000, 24, 6, 9_000_000, 25, true)
            })
            .Responde("GET", "api/categorias", new[] { Datos.Categoria() })
            .Responde("GET", "api/hogares/mio", Datos.Grupo());

        var pantalla = RenderComponent<Deudas>();
        pantalla.FindAll("button.icon-btn").Last().Click();

        Assert.NotNull(Confirmacion.Pendiente);
        Assert.Contains("6 cuotas", Confirmacion.Pendiente!.Detalle);
        Assert.DoesNotContain(Servidor.Llamadas, l => l.Metodo == "DELETE");
    }

    /// <summary>
    /// Y confirmando, sí borra: una pregunta que no deja completar la acción
    /// sería peor que no preguntar.
    /// </summary>
    [Fact]
    public void ConfirmandoSiBorra()
    {
        Servidor
            .Responde("GET", "api/tarjetas-credito", new[] { new TarjetaCreditoResponse(3, "Visa", 15, true) })
            .Responde("GET", "api/tarjetas-credito/3/saldo-pendiente", new SaldoPendienteResponse(0, null))
            .Responde("DELETE", "api/tarjetas-credito/3", null, HttpStatusCode.NoContent);

        var pantalla = RenderComponent<TarjetasCredito>();
        pantalla.FindAll("button.icon-btn").Last().Click();
        ConfirmarLoQuePregunte();
        pantalla.WaitForState(() => Servidor.Llamadas.Any(l => l.Metodo == "DELETE"));

        Assert.Contains(Servidor.Llamadas, l => l is { Metodo: "DELETE", Ruta: "api/tarjetas-credito/3" });
    }

    // ---------- La hoja ----------

    [Fact]
    public void LaHojaMuestraLaPreguntaYElVerboDeLaAccion()
    {
        var hoja = RenderComponent<MoneyBack.Web.Shared.HojaDeConfirmacion>();
        Assert.Empty(hoja.FindAll(".sheet"));

        _ = Confirmacion.PreguntarAsync(new Confirmacion(
            "¿Borrar este gasto de $47.300?", "Mercado. No se puede deshacer.", "Sí, borrarlo"));
        hoja.WaitForAssertion(() => Assert.Single(hoja.FindAll(".sheet")));

        Assert.Contains("¿Borrar este gasto de $47.300?", hoja.Markup);
        Assert.Contains("No se puede deshacer", hoja.Markup);
        // El botón dice el verbo, no "Aceptar".
        Assert.Contains("Sí, borrarlo", hoja.Find(".btn-danger-solid").TextContent);
    }

    /// <summary>
    /// Tocar por fuera equivale a cancelar, nunca a confirmar: es el gesto
    /// con el que uno sale de algo que abrió sin querer.
    /// </summary>
    [Fact]
    public async Task TocarPorFueraEsCancelar()
    {
        var hoja = RenderComponent<MoneyBack.Web.Shared.HojaDeConfirmacion>();
        var pregunta = Confirmacion.PreguntarAsync(new Confirmacion("¿Seguro?"));
        hoja.WaitForAssertion(() => Assert.Single(hoja.FindAll(".sheet-backdrop")));

        hoja.Find(".sheet-backdrop").Click();

        Assert.False(await pregunta);
    }

    /// <summary>
    /// El botón que borra va arriba y el de cancelar abajo, pegado al borde:
    /// si estuvieran al revés, el pulgar cae primero en el que borra.
    /// </summary>
    [Fact]
    public void ElBotonDeCancelarEsElUltimoYElMasCercaDelPulgar()
    {
        var hoja = RenderComponent<MoneyBack.Web.Shared.HojaDeConfirmacion>();
        _ = Confirmacion.PreguntarAsync(new Confirmacion("¿Seguro?", TextoConfirmar: "Sí, borrarlo"));
        hoja.WaitForAssertion(() => Assert.Single(hoja.FindAll(".sheet")));

        var botones = hoja.FindAll(".sheet button");
        Assert.Equal("Cancelar", botones.Last().TextContent.Trim());
    }
}
