using MoneyBack.Api.Domain.Subsidios;
using MoneyBack.Api.Models.Subsidios;

namespace MoneyBack.Api.Tests;

/// <summary>
/// El simulador anterior le pedía a la persona el monto de su subsidio y se lo
/// devolvía sumado. Acá lo que se prueba es lo contrario: que con solo el
/// valor de la vivienda y el ingreso, la app diga algo que la persona no
/// sabía — cuánto le prestarían, con qué cuota, y si esa cuota le cabe.
///
/// Las cifras de mercado viven en ParametrosVivienda y cambian; estas pruebas
/// verifican las reglas, no los números exactos, para que actualizar una tasa
/// no obligue a reescribir la suite.
/// </summary>
public class PlanViviendaTests
{
    private const decimal Smmlv = 1_750_905m;

    private static PlanVivienda_Resultado Calcular(
        decimal valorVivienda, decimal ingreso, bool afiliadoCaja = true, decimal ahorro = 0) =>
        PlanVivienda.Calcular(valorVivienda, ingreso, Smmlv, TipoTopeVis.Decreto1467, afiliadoCaja, ahorro);

    [Fact]
    public void UnaViviendaBajoElTopeEsVis_YUnaPorEncimaNoLoEs()
    {
        // Tope Decreto 1467: 150 SMMLV.
        Assert.Equal("VIS", Calcular(150m * Smmlv, 4_000_000m).Clasificacion);
        Assert.Equal("No VIS", Calcular(150m * Smmlv + 1m, 4_000_000m).Clasificacion);

        // VIP: 90 SMMLV.
        Assert.Equal("VIP", Calcular(90m * Smmlv, 4_000_000m).Clasificacion);
    }

    [Fact]
    public void ElSubsidioDeCajaDependeDelTramoDeIngreso()
    {
        var bajo = Calcular(100m * Smmlv, 1.5m * Smmlv);
        var medio = Calcular(100m * Smmlv, 3m * Smmlv);
        var alto = Calcular(100m * Smmlv, 6m * Smmlv);

        Assert.Equal(30m * Smmlv, Caja(bajo).Monto);
        Assert.Equal(20m * Smmlv, Caja(medio).Monto);
        Assert.Equal(0m, Caja(alto).Monto);
        Assert.False(Caja(alto).EsSeguro);
    }

    /// <summary>
    /// Mi Casa Ya aparece pero no suma. En 2026 quedan cupos remanentes, y
    /// contar treinta millones que no van a llegar llevaría a alguien a
    /// comprometerse con una cuota inicial que no puede pagar.
    /// </summary>
    [Fact]
    public void MiCasaYaSeInformaPeroNoSeCuentaComoPlataSegura()
    {
        var plan = Calcular(100m * Smmlv, 2m * Smmlv);

        var miCasaYa = plan.Subsidios.Single(s => s.Nombre == "Mi Casa Ya");
        Assert.True(miCasaYa.Monto > 0);
        Assert.False(miCasaYa.EsSeguro);

        // El total seguro es solo el de la caja.
        Assert.Equal(Caja(plan).Monto, plan.TotalSubsidiosSeguros);
    }

    [Fact]
    public void SinAfiliacionACaja_SeDiceQueVayanAAveriguarlo()
    {
        var plan = Calcular(100m * Smmlv, 2m * Smmlv, afiliadoCaja: false);

        Assert.Equal(0m, plan.TotalSubsidiosSeguros);
        Assert.Contains("caja de compensación", Caja(plan).Detalle);
    }

    [Fact]
    public void UnaViviendaNoVisNoRecibeNingunSubsidio()
    {
        var plan = Calcular(200m * Smmlv, 10m * Smmlv);

        Assert.Equal(0m, plan.TotalSubsidiosSeguros);
        Assert.All(plan.Subsidios, s => Assert.False(s.EsSeguro));
    }

    /// <summary>
    /// Es el corazón de "¿me alcanza?": la cuota contra el ingreso. La regla
    /// del 30% es la que usan de verdad las entidades para aprobar.
    /// </summary>
    [Fact]
    public void DiceSiLaCuotaCabeEnElIngreso()
    {
        var holgado = Calcular(90m * Smmlv, 12_000_000m);
        var apretado = Calcular(90m * Smmlv, 1_500_000m);

        Assert.All(holgado.Opciones, o => Assert.True(o.CabeEnElIngreso));
        Assert.All(apretado.Opciones, o => Assert.False(o.CabeEnElIngreso));
    }

    [Fact]
    public void LosSubsidiosBajanLoQueHayQuePedirPrestado()
    {
        var conCaja = Calcular(100m * Smmlv, 1.5m * Smmlv, afiliadoCaja: true);
        var sinCaja = Calcular(100m * Smmlv, 1.5m * Smmlv, afiliadoCaja: false);

        var deudaConCaja = conCaja.Opciones.First(o => o.Entidad == "Banco").MontoAFinanciar;
        var deudaSinCaja = sinCaja.Opciones.First(o => o.Entidad == "Banco").MontoAFinanciar;

        Assert.True(deudaConCaja < deudaSinCaja);
    }

    /// <summary>
    /// Lo que ya llevan ahorrado sale de sus metas, y se descuenta de lo que
    /// les falta. Que la respuesta cambie según lo que llevan es lo que la
    /// hace suya y no un folleto.
    /// </summary>
    [Fact]
    public void LoYaAhorradoSeDescuentaDeLoQueFaltaParaLaCuotaInicial()
    {
        var sinAhorro = Calcular(200m * Smmlv, 15_000_000m, ahorro: 0);
        var conAhorro = Calcular(200m * Smmlv, 15_000_000m, ahorro: 50_000_000m);

        var faltaAntes = sinAhorro.Opciones.First().LeFaltaParaLaCuotaInicial;
        var faltaDespues = conAhorro.Opciones.First().LeFaltaParaLaCuotaInicial;

        Assert.Equal(faltaAntes - 50_000_000m, faltaDespues);
    }

    /// <summary>
    /// La tasa llega efectiva anual, como se publica en Colombia. Dividirla
    /// entre 12 daría una cuota más baja que la real — el error que no se
    /// puede cometer en una herramienta que sirve para decidir una compra.
    /// </summary>
    [Fact]
    public void LaCuotaUsaLaTasaMensualEquivalente_NoLaAnualDivididaEnDoce()
    {
        var capital = 100_000_000m;
        var cuota = PlanVivienda.CuotaMensual(capital, 0.12m, 240);

        var mensualEquivalente = (decimal)(Math.Pow(1.12, 1.0 / 12) - 1);
        var ingenua = 0.12m / 12;

        Assert.True(mensualEquivalente < ingenua);

        // Con la tasa correcta la cuota es menor que con la ingenua, y ninguna
        // de las dos puede ser menor que repartir el capital sin intereses.
        var conIngenua = capital * ingenua * (decimal)Math.Pow(1 + (double)ingenua, 240)
            / ((decimal)Math.Pow(1 + (double)ingenua, 240) - 1);

        Assert.True(cuota < conIngenua);
        Assert.True(cuota > capital / 240);
    }

    [Fact]
    public void UnCapitalDeCeroNoGeneraCuota()
    {
        Assert.Equal(0m, PlanVivienda.CuotaMensual(0m, 0.12m, 240));
    }

    [Fact]
    public void ElResultadoDiceDesdeCuandoSonLosDatos()
    {
        var plan = Calcular(100m * Smmlv, 3_000_000m);

        // Sin esto, alguien puede tomar una decisión de cien millones con
        // tasas del año pasado sin saberlo.
        Assert.Equal(ParametrosVivienda.VigenteDesde, plan.DatosVigentesDesde);
    }

    private static AyudaDisponible Caja(PlanVivienda_Resultado plan) =>
        plan.Subsidios.Single(s => s.Nombre == "Subsidio de caja de compensación");
}
