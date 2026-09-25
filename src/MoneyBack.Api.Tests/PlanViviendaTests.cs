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
        decimal valorVivienda, decimal ingreso, bool afiliadoCaja = true,
        decimal ahorro = 0, bool bogota = false, decimal cesantias = 0, bool renovacion = false,
        int? escrituracion = null) =>
        PlanVivienda.Calcular(valorVivienda, ingreso, Smmlv, TipoTopeVis.Decreto1467,
            afiliadoCaja, bogota, cesantias, ahorro, renovacion, escrituracion);

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
    /// Por encima de 2 SMMLV no hay concurrencia, así que Mi Casa Ya se
    /// informa pero no se suma: quedan pocos cupos y contar veinte millones
    /// que no van a llegar llevaría a alguien a comprometerse con una cuota
    /// inicial que no puede pagar.
    /// </summary>
    [Fact]
    public void SinConcurrencia_MiCasaYaSeInformaPeroNoSeCuentaComoSeguro()
    {
        var plan = Calcular(100m * Smmlv, 3m * Smmlv);

        var miCasaYa = plan.Ayudas.Single(s => s.Nombre == "Mi Casa Ya");
        Assert.True(miCasaYa.Monto > 0);
        Assert.False(miCasaYa.EsSeguro);
        Assert.Equal(Caja(plan).Monto, plan.TotalSubsidiosSeguros);
    }

    /// <summary>
    /// Hasta 2 SMMLV el subsidio de la caja se puede sumar con Mi Casa Ya
    /// hasta 50 salarios mínimos. Esta es la pieza que faltaba: sin ella, un
    /// hogar de dos mínimos recibía un "imposible" que contradice lo que pasa
    /// en la realidad.
    /// </summary>
    [Fact]
    public void HastaDosSalariosMinimos_LaCajaYMiCasaYaSeSuman()
    {
        var plan = Calcular(90m * Smmlv, 2m * Smmlv);

        var concurrencia = plan.Ayudas.Single(a => a.Nombre.StartsWith("Mi Casa Ya (en concurrencia)"));
        Assert.True(concurrencia.EsSeguro);

        Assert.Equal(ParametrosVivienda.TopeConcurrenciaSmmlv * Smmlv, plan.TotalSubsidiosSeguros);
    }

    /// <summary>
    /// El caso que motivó reconstruir todo esto: dos salarios mínimos
    /// comprando una VIP en Bogotá. La versión anterior decía que era
    /// imposible por banco y por FNA, y en la calle hay gente haciéndolo.
    /// </summary>
    [Fact]
    public void DosSalariosMinimosEnBogotaSiAlcanzanParaUnaVip()
    {
        var plan = Calcular(90m * Smmlv, 2m * Smmlv, bogota: true);

        Assert.Equal("VIP", plan.Clasificacion);

        // Concurrencia (50 SMMLV) + distrital de Bogotá (10 SMMLV).
        Assert.Equal(60m * Smmlv, plan.TotalSubsidiosSeguros);

        Assert.All(plan.Opciones, o => Assert.True(o.CabeEnElIngreso,
            $"{o.Entidad}: cuota {o.CuotaMensual:N0} = {o.ProporcionDelIngreso:P0} del ingreso"));
    }

    /// <summary>
    /// La cobertura no es un adorno en una lista: baja la cuota de verdad.
    /// Antes se mostraba como "ayuda" y nunca se aplicaba, que es justo lo
    /// que hacía ver imposible lo que no lo es.
    /// </summary>
    [Fact]
    public void LaCoberturaALaTasaBajaLaCuotaDeVerdad()
    {
        var plan = Calcular(120m * Smmlv, 5m * Smmlv);
        var banco = plan.Opciones.Single(o => o.Entidad == "Banco");

        Assert.True(banco.TieneCobertura);
        Assert.True(banco.TasaConCobertura < banco.TasaEfectivaAnual);
        Assert.True(banco.CuotaMensual < banco.CuotaDespuesDeLaCobertura);
    }

    /// <summary>
    /// Y se dice cuánto sube después, porque sube. Enseñar solo la cuota
    /// barata sería mentir por omisión justo en el número con el que alguien
    /// firma una deuda de treinta años.
    /// </summary>
    [Fact]
    public void SeAvisaSiLaCuotaDejaDeCaberCuandoSeAcabaLaCobertura()
    {
        // Ingreso elegido a propósito entre las dos cuotas: la subsidiada
        // cabe en el 30%, la plena no.
        var plan = Calcular(150m * Smmlv, 5_500_000m);
        var banco = plan.Opciones.Single(o => o.Entidad == "Banco");

        Assert.True(banco.CuotaDespuesDeLaCobertura > banco.CuotaMensual);
        Assert.NotEqual(banco.CabeEnElIngreso, banco.CabeCuandoSubaLaCuota);
    }

    [Fact]
    public void LasCesantiasCuentanParaLaCuotaInicial()
    {
        var sinCesantias = Calcular(200m * Smmlv, 15_000_000m);
        var conCesantias = Calcular(200m * Smmlv, 15_000_000m, cesantias: 20_000_000m);

        Assert.Equal(
            sinCesantias.Opciones.First().LeFaltaParaLaCuotaInicial - 20_000_000m,
            conCesantias.Opciones.First().LeFaltaParaLaCuotaInicial);
    }

    /// <summary>
    /// Los créditos VIS llegan a 30 años. Calcularlos a 20 —como se hacía
    /// antes— inflaba la cuota y volvía imposible en el papel algo que en la
    /// realidad la gente está pagando.
    /// </summary>
    [Fact]
    public void LaViviendaSocialSeCalculaATreintaAnios()
    {
        Assert.All(Calcular(90m * Smmlv, 3_000_000m).Opciones, o => Assert.Equal(360, o.PlazoMeses));
        Assert.All(Calcular(200m * Smmlv, 15_000_000m).Opciones, o => Assert.Equal(240, o.PlazoMeses));
    }

    [Fact]
    public void EnBogotaSeSumaElSubsidioDistrital()
    {
        var enBogota = Calcular(100m * Smmlv, 3m * Smmlv, bogota: true);
        var fuera = Calcular(100m * Smmlv, 3m * Smmlv, bogota: false);

        Assert.True(enBogota.TotalSubsidiosSeguros > fuera.TotalSubsidiosSeguros);

        // A quien no está en Bogotá se le dice que averigüe el de su ciudad,
        // en vez de dejarlo creyendo que no existe.
        Assert.Contains(fuera.Ayudas, a => a.Nombre.Contains("su ciudad"));
    }

    [Fact]
    public void SinAfiliacionACaja_SeDiceQueVayanAAveriguarlo()
    {
        var plan = Calcular(100m * Smmlv, 2m * Smmlv, afiliadoCaja: false);

        Assert.Equal(0m, plan.TotalSubsidiosSeguros);
        Assert.False(Caja(plan).EsSeguro);
        Assert.Contains("cotiza a una caja", Caja(plan).Detalle);
    }

    [Fact]
    public void UnaViviendaNoVisNoRecibeNingunSubsidio()
    {
        var plan = Calcular(200m * Smmlv, 10m * Smmlv);

        Assert.Equal(0m, plan.TotalSubsidiosSeguros);
        Assert.All(plan.Ayudas, s => Assert.False(s.EsSeguro));
    }

    /// <summary>
    /// Es el corazón de "¿me alcanza?": la cuota contra el ingreso. La regla
    /// del 30% es la que usan de verdad las entidades para aprobar.
    /// </summary>
    [Fact]
    public void DiceSiLaCuotaCabeEnElIngreso()
    {
        var holgado = Calcular(90m * Smmlv, 12_000_000m);

        // Una No VIS cara con un ingreso bajo: acá no hay subsidio, ni
        // cobertura, ni plazo de 30 años que lo salve.
        var imposible = Calcular(200m * Smmlv, 3_000_000m);

        Assert.All(holgado.Opciones, o => Assert.True(o.CabeEnElIngreso));
        Assert.All(imposible.Opciones, o => Assert.False(o.CabeEnElIngreso));
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
        plan.Ayudas.Single(s => s.Nombre == "Subsidio de caja de compensación");

    /// <summary>
    /// En renovación urbana el tope VIS sube de 150 a 175 SMMLV. La misma
    /// vivienda pasa de quedarse sin nada a tener todos los subsidios, que es
    /// una diferencia de decenas de millones — y en Bogotá buena parte de los
    /// proyectos nuevos están en esas zonas.
    /// </summary>
    [Fact]
    public void EnRenovacionUrbanaElTopeVisSubeYLaMismaViviendaSiCalifica()
    {
        var valor = 160m * Smmlv;

        Assert.Equal("No VIS", Calcular(valor, 3m * Smmlv).Clasificacion);
        Assert.Equal("VIS", Calcular(valor, 3m * Smmlv, renovacion: true).Clasificacion);

        Assert.True(Calcular(valor, 3m * Smmlv, renovacion: true).TotalSubsidiosSeguros > 0);
    }

    /// <summary>
    /// Una No VIS no se queda sin nada: el FRECH No VIS cubre 42 SMMLV de
    /// intereses en 84 meses. Antes esta herramienta le daba cero ayudas, y
    /// eso no es cierto.
    /// </summary>
    [Fact]
    public void UnaNoVisRecibeElFrechNoVisYEsoLeBajaLaCuota()
    {
        var plan = Calcular(200m * Smmlv, 15_000_000m);
        var fna = plan.Opciones.Single(o => o.Entidad == "FNA");

        var subsidioMensual = ParametrosVivienda.FrechNoVisTotalSmmlv * Smmlv / ParametrosVivienda.MesesCoberturaTasa;
        Assert.Equal(Math.Round(fna.CuotaDespuesDeLaCobertura - subsidioMensual), fna.CuotaMensual);
    }

    [Fact]
    public void UnaViviendaPorEncimaDelTopeDelFrechNoVisNoLoRecibe()
    {
        var plan = Calcular(600m * Smmlv, 40_000_000m);
        var fna = plan.Opciones.Single(o => o.Entidad == "FNA");

        Assert.Equal(fna.CuotaDespuesDeLaCobertura, fna.CuotaMensual);
    }

    /// <summary>
    /// "No te alcanza" no dice qué hacer. "Les faltan ochocientos mil al mes"
    /// sí: se puede buscar más barato, sumar un codeudor, o esperar.
    /// </summary>
    [Fact]
    public void DiceCuantoTendrianQueGanarParaQueSeLoAprueben()
    {
        var plan = Calcular(200m * Smmlv, 3_000_000m);

        foreach (var opcion in plan.Opciones)
        {
            Assert.True(opcion.IngresoMinimoRequerido > 3_000_000m);

            // Con ese ingreso exacto, la cuota daría justo el 30%.
            Assert.Equal(0.30m, Math.Round(opcion.CuotaMensual / opcion.IngresoMinimoRequerido, 2));
        }
    }

    /// <summary>
    /// Ante una vivienda que pasa el tope, lo útil no es "no aplica": es
    /// cuánto cuesta esa decisión y hasta dónde podrían buscar sin perder las
    /// ayudas. Eso cambia en qué proyectos miran.
    /// </summary>
    [Fact]
    public void SiLaViviendaPasaElTope_SeMuestraQueSePierdeYHastaDondeBuscar()
    {
        var plan = Calcular(200m * Smmlv, 4m * Smmlv, bogota: true);

        var comparacion = Assert.IsType<ComparacionVis>(plan.SiFueraVis);
        Assert.Equal(150m * Smmlv, comparacion.ValorMaximoVis);
        Assert.True(comparacion.SubsidiosQuePerdieron > 0);
        Assert.True(comparacion.CuotaMensual < plan.Opciones.Min(o => o.CuotaMensual));
    }

    [Fact]
    public void SiLaViviendaYaEsVis_NoHayNadaQueComparar()
    {
        Assert.Null(Calcular(100m * Smmlv, 3m * Smmlv).SiFueraVis);
    }

    /// <summary>
    /// El tope se mide al escriturar, no al firmar la promesa. Un proyecto
    /// sobre planos que entrega en tres años se compara contra el tope de
    /// entonces — es lo que explica que existan VIS de trescientos y pico de
    /// millones, y comparar contra el tope de hoy descartaba proyectos que sí
    /// califican.
    /// </summary>
    [Fact]
    public void ElTopeSeMideEnElAnioDeEscrituracion_NoHoy()
    {
        var valor = 165m * Smmlv;
        var dentroDeTresAnios = DateTime.UtcNow.Year + 3;

        Assert.Equal("No VIS", Calcular(valor, 4m * Smmlv).Clasificacion);
        Assert.Equal("VIS", Calcular(valor, 4m * Smmlv, escrituracion: dentroDeTresAnios).Clasificacion);
    }

    [Fact]
    public void UnaEscrituracionEnElPasadoNoEncogeElTope()
    {
        // Nadie escritura hacia atrás, pero un año viejo escrito por error no
        // debe volver No VIS algo que sí califica hoy.
        var valor = 140m * Smmlv;

        Assert.Equal(
            Calcular(valor, 4m * Smmlv).TopeVis,
            Calcular(valor, 4m * Smmlv, escrituracion: 2020).TopeVis);
    }

    // --- El techo de compra: la pregunta al revés ---

    /// <summary>
    /// Preguntando de a un proyecto uno se entera de que no alcanza después
    /// de haberse ilusionado. El techo permite filtrar antes de mirar.
    /// </summary>
    [Fact]
    public void DiceHastaCuantoPuedenComprar_YEseTechoCrecePorCadaAyuda()
    {
        var solos = Calcular(100m * Smmlv, 4_000_000m, afiliadoCaja: false);
        var conCaja = Calcular(100m * Smmlv, 4_000_000m, afiliadoCaja: true);
        var conTodo = Calcular(100m * Smmlv, 4_000_000m, afiliadoCaja: true, bogota: true, cesantias: 10_000_000m);

        Assert.True(solos.Techo.PrecioMaximo < conCaja.Techo.PrecioMaximo);
        Assert.True(conCaja.Techo.PrecioMaximo < conTodo.Techo.PrecioMaximo);
    }

    /// <summary>
    /// Pasarse del tope no compra más casa: cuesta los subsidios, y el
    /// crédito extra nunca alcanza a reponerlos. Por eso el techo se corta
    /// ahí y se dice que fue por eso.
    /// </summary>
    [Fact]
    public void ElTechoSeCortaEnElTopeVis_YSeDiceQueFuePorEso()
    {
        var plan = Calcular(100m * Smmlv, 30_000_000m, bogota: true);

        Assert.True(plan.Techo.LimitadoPorElTopeVis);
        Assert.Equal(Math.Floor(plan.TopeVis), plan.Techo.PrecioMaximo);
    }

    [Fact]
    public void ConIngresoBajoElTechoLoMarcaElIngreso_NoElTope()
    {
        var plan = Calcular(100m * Smmlv, 2_000_000m);

        Assert.False(plan.Techo.LimitadoPorElTopeVis);
        Assert.True(plan.Techo.PrecioMaximo < plan.TopeVis);
    }

    /// <summary>
    /// Ida y vuelta: una vivienda al precio del techo tiene que caber en el
    /// ingreso. Si no cerrara, el techo estaría mintiendo.
    /// </summary>
    [Fact]
    public void UnaViviendaAlPrecioDelTechoSiCabeEnElIngreso()
    {
        const decimal ingreso = 5_000_000m;
        var techo = Calcular(100m * Smmlv, ingreso, bogota: true).Techo.PrecioMaximo;

        var alLimite = Calcular(techo, ingreso, bogota: true);
        Assert.Contains(alLimite.Opciones, o => o.CabeEnElIngreso);
    }

    // --- El escalón de los 2 SMMLV ---

    /// <summary>
    /// Por debajo de 2 SMMLV la caja da 30 salarios mínimos y se le suma Mi
    /// Casa Ya; por encima, la caja baja a 20 y la concurrencia desaparece.
    /// Treinta salarios mínimos de diferencia por ganar un peso de más: nadie
    /// puede decidir bien sin saber que ese escalón existe.
    /// </summary>
    [Fact]
    public void AvisaCuandoElIngresoEstaJustoPorEncimaDelUmbralDeConcurrencia()
    {
        var ingreso = 2.1m * Smmlv;
        var plan = Calcular(100m * Smmlv, ingreso, bogota: true);

        var umbral = Assert.IsType<AlertaUmbral>(plan.Umbral);
        Assert.Equal(Math.Floor(2m * Smmlv), umbral.IngresoDelUmbral);
        Assert.Equal(Math.Ceiling(ingreso - 2m * Smmlv), umbral.SeExcedenPor);
        Assert.Equal(30m * Smmlv, umbral.SubsidiosSiEstuvieranDebajo);
    }

    [Fact]
    public void NoAvisaDelUmbralSiYaEstanDebajo()
    {
        Assert.Null(Calcular(100m * Smmlv, 1.8m * Smmlv, bogota: true).Umbral);
    }

    /// <summary>
    /// A cuatro salarios mínimos el dato es ruido: el umbral está lejos y no
    /// hay nada que hacer con esa información.
    /// </summary>
    [Fact]
    public void NoAvisaDelUmbralSiEstanDemasiadoLejos()
    {
        Assert.Null(Calcular(100m * Smmlv, 4m * Smmlv, bogota: true).Umbral);
    }

    [Fact]
    public void ElAvisoDelUmbralCuentaLoQueRealmenteGanarian()
    {
        // Sin caja no hay concurrencia que perder, así que no hay nada que avisar.
        Assert.Null(Calcular(100m * Smmlv, 2.1m * Smmlv, afiliadoCaja: false).Umbral);
    }
}
