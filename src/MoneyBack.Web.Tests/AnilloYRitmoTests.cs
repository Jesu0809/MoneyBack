using MoneyBack.Web.Models;
using MoneyBack.Web.Pages;
using MoneyBack.Web.Shared;
using MoneyBack.Web.Tests.Infra;

namespace MoneyBack.Web.Tests;

public class AnilloYRitmoTests : PruebaDePantalla
{
    // ---------- El anillo ----------

    [Fact]
    public void ElAnilloSeLlenaHastaElPorcentajeQueSeLePasa()
    {
        var anillo = RenderComponent<AnilloProgreso>(p => p.Add(a => a.Porcentaje, 37.5m));

        anillo.WaitForAssertion(() => Assert.Contains("37.5%", anillo.Markup));
    }

    /// <summary>
    /// Un porcentaje mayor a 100 (se puede pasar de la meta) no puede pintar
    /// un arco dando la segunda vuelta.
    /// </summary>
    [Fact]
    public void ElAnilloNoDaLaVueltaSiSePasaronDeLaMeta()
    {
        var anillo = RenderComponent<AnilloProgreso>(p => p.Add(a => a.Porcentaje, 140m));

        anillo.WaitForAssertion(() => Assert.Contains("100%", anillo.Markup));
    }

    /// <summary>
    /// Dos anillos en la misma página no pueden compartir el id del
    /// gradiente: el segundo se pintaría transparente.
    /// </summary>
    [Fact]
    public void DosAnillosNoCompartenElIdDelGradiente()
    {
        var uno = RenderComponent<AnilloProgreso>(p => p.Add(a => a.Porcentaje, 20m));
        var otro = RenderComponent<AnilloProgreso>(p => p.Add(a => a.Porcentaje, 20m));

        Assert.NotEqual(
            uno.Find("linearGradient").GetAttribute("id"),
            otro.Find("linearGradient").GetAttribute("id"));
    }

    /// <summary>
    /// El arco se dibuja con stroke-dashoffset y tiene que salir con punto
    /// decimal, no con coma: en un teléfono en español —o sea, el de ellos—
    /// "265,47" es un valor inválido y el arco no se pinta.
    ///
    /// La cultura se cambia en un hilo propio: hacerlo en el hilo de la
    /// prueba deja el cambio pegado en el hilo del pool al primer await y
    /// contamina lo que corra después (ya rompió otra prueba así).
    /// </summary>
    [Fact]
    public void ElArcoSeEscribeConPuntoDecimalSiempre()
    {
        string? offset = null;
        Exception? falla = null;

        var hilo = new Thread(() =>
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("es-CO");
            try
            {
                using var ctx = new TestContext();
                var anillo = ctx.RenderComponent<AnilloProgreso>(p => p.Add(a => a.Porcentaje, 33m));
                anillo.WaitForAssertion(() =>
                    Assert.NotEqual("0", anillo.FindAll("circle").Last().GetAttribute("stroke-dashoffset")));
                offset = anillo.FindAll("circle").Last().GetAttribute("stroke-dashoffset");
            }
            catch (Exception e) { falla = e; }
        });
        hilo.Start();
        hilo.Join();

        if (falla is not null) throw falla;
        Assert.NotNull(offset);
        Assert.DoesNotContain(",", offset);
    }

    // ---------- El ritmo de ahorro ----------

    private static MovimientoResponse Aporte(decimal monto, int diasAtras) =>
        new(1, 1, "Ana", TipoMovimiento.Aporte, monto, DateTime.UtcNow.AddDays(-diasAtras), null, false);

    private void MetaCon(decimal actual, decimal objetivo, params MovimientoResponse[] movimientos)
    {
        var meta = Datos.Meta(10, "Cuota inicial", actual: actual, objetivo: objetivo);
        Servidor.Responde("GET", "api/metas/10", new MetaDetalleResponse(
            meta.Id, meta.HogarId, meta.Nombre, meta.Icono, false, 100,
            objetivo, actual, objetivo == 0 ? 0 : actual / objetivo * 100,
            null, true, DateTime.UtcNow.AddYears(-1), [], movimientos.ToList()));
    }

    [Fact]
    public void ConHistorialSuficiente_DiceCuantoFaltaAEseRitmo()
    {
        // 6.000.000 en 6 meses = 1.000.000 al mes. Faltan 24.000.000 = 24 meses.
        MetaCon(6_000_000, 30_000_000, Aporte(3_000_000, 180), Aporte(3_000_000, 30));

        var pantalla = RenderComponent<MetaDetalle>(p => p.Add(m => m.MetaId, 10));

        Assert.Contains("por mes", pantalla.Markup);
        Assert.Contains("años", pantalla.Markup);
    }

    /// <summary>
    /// Con un solo aporte no hay ritmo que medir. Inventar una proyección
    /// con un dato sería peor que no decir nada: acá se decide sobre una
    /// compra de cien millones.
    /// </summary>
    [Fact]
    public void ConUnSoloAporte_NoProyectaNada()
    {
        MetaCon(3_000_000, 30_000_000, Aporte(3_000_000, 200));

        var pantalla = RenderComponent<MetaDetalle>(p => p.Add(m => m.MetaId, 10));

        Assert.DoesNotContain("por mes", pantalla.Markup);
    }

    [Fact]
    public void ConTodoAportadoEsteMes_TampocoProyecta()
    {
        MetaCon(2_000_000, 30_000_000, Aporte(1_000_000, 5), Aporte(1_000_000, 2));

        var pantalla = RenderComponent<MetaDetalle>(p => p.Add(m => m.MetaId, 10));

        Assert.DoesNotContain("por mes", pantalla.Markup);
    }

    [Fact]
    public void AlLlegarALaMeta_LoCelebraEnVezDeProyectar()
    {
        MetaCon(30_000_000, 30_000_000, Aporte(15_000_000, 300), Aporte(15_000_000, 40));

        var pantalla = RenderComponent<MetaDetalle>(p => p.Add(m => m.MetaId, 10));

        Assert.Contains("Ya llegaron a la meta", pantalla.Markup);
    }

    /// <summary>
    /// Los retiros no cuentan como ritmo de aporte, pero sí bajan el monto
    /// actual — que es de donde sale la proyección.
    /// </summary>
    [Fact]
    public void UnRetiroNoCuentaComoAporte()
    {
        var retiro = new MovimientoResponse(2, 1, "Ana", TipoMovimiento.Retiro, 500_000,
            DateTime.UtcNow.AddDays(-10), null, false);
        MetaCon(1_000_000, 30_000_000, Aporte(1_500_000, 200), retiro);

        var pantalla = RenderComponent<MetaDetalle>(p => p.Add(m => m.MetaId, 10));

        // Solo hay un aporte: no alcanza para hablar de ritmo.
        Assert.DoesNotContain("por mes", pantalla.Markup);
    }
}
