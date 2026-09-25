using MoneyBack.Api.Models.Subsidios;

namespace MoneyBack.Api.Domain.Subsidios;

/// <summary>
/// Responde "¿me alcanza para esta vivienda, y cómo la pago?".
///
/// La primera versión decía que era imposible para un hogar de dos salarios
/// mínimos, y eso contradecía la realidad: en Colombia hay gente comprando
/// VIS con ese ingreso. El error no era uno solo, eran cinco, y todos
/// empujaban en la misma dirección:
///
///   1. Calculaba a 20 años cuando los créditos VIS llegan a 30.
///   2. Listaba la cobertura a la tasa como "ayuda" y nunca la aplicaba a la
///      cuota, que es exactamente lo que esa cobertura hace.
///   3. No permitía concurrencia: contaba solo el subsidio de la caja,
///      cuando un hogar de hasta 2 SMMLV puede sumarle Mi Casa Ya.
///   4. No conocía los subsidios distritales.
///   5. No contaba las cesantías, que para muchos hogares SON la cuota
///      inicial.
///
/// Corregidos los cinco, el mismo hogar que antes recibía un "no" pasa a
/// tener una cuota que le cabe. La conclusión práctica: cuando una
/// herramienta dice que algo es imposible y la realidad dice lo contrario,
/// el error está en la herramienta.
/// </summary>
public static class PlanVivienda
{
    public static PlanVivienda_Resultado Calcular(
        decimal valorVivienda,
        decimal ingresoMensual,
        decimal smmlv,
        TipoTopeVis tipoTope,
        bool afiliadoCaja,
        bool viveEnBogota,
        decimal cesantias,
        decimal ahorroActual,
        bool esRenovacionUrbana = false,
        int? anioEscrituracion = null)
    {
        // En zonas de renovación urbana el tope VIS sube a 175 SMMLV. Importa
        // en Bogotá, donde buena parte de los proyectos nuevos están en esas
        // zonas: son casi cuarenta millones más de margen para seguir siendo
        // VIS, es decir, para no perder los subsidios.
        if (esRenovacionUrbana) tipoTope = TipoTopeVis.RenovacionUrbana;

        // El tope se mide a la fecha de escrituración. Un proyecto sobre
        // planos que entrega en 2029 se compara contra el tope de 2029, no
        // contra el de hoy: por eso existen VIS de trescientos y pico de
        // millones, y por eso comparar contra el tope actual descartaba
        // proyectos que sí califican.
        var aniosPorDelante = Math.Max(0, (anioEscrituracion ?? DateTime.UtcNow.Year) - DateTime.UtcNow.Year);
        var smmlvAlEscriturar = smmlv * (decimal)Math.Pow(
            1 + (double)ParametrosVivienda.CrecimientoAnualSmmlvEstimado, aniosPorDelante);

        var topeVis = TopesVis.TopeEnPesos(tipoTope, smmlvAlEscriturar);
        var topeVip = TopesVis.TopeEnPesos(TipoTopeVis.Vip, smmlvAlEscriturar);

        var esVip = valorVivienda <= topeVip;
        var esVis = valorVivienda <= topeVis;
        var ingresoEnSmmlv = smmlv > 0 ? ingresoMensual / smmlv : 0;

        var ayudas = CalcularAyudas(esVis, esVip, ingresoEnSmmlv, smmlv, afiliadoCaja, viveEnBogota);
        var totalSubsidios = ayudas.Where(a => a.EsSeguro).Sum(a => a.Monto);

        // Las cesantías se pueden retirar para comprar vivienda, así que para
        // la cuota inicial cuentan igual que el ahorro. Dejarlas fuera hacía
        // ver como inalcanzable una cuota inicial que muchos hogares ya tienen
        // reunida sin saberlo.
        var disponibleParaInicial = ahorroActual + cesantias;

        var opciones = CalcularOpciones(
            valorVivienda, ingresoMensual, esVis, esVip, ingresoEnSmmlv, totalSubsidios,
            disponibleParaInicial, smmlv);

        // Si la vivienda pasa el tope, lo más útil que se puede decir no es
        // "no aplica": es cuánto cuesta esa decisión y hasta dónde podrían
        // buscar sin perder las ayudas. Eso cambia en qué proyectos miran.
        var comparacion = (esVis || esVip)
            ? null
            : CompararConLaMejorVis(topeVis, ingresoMensual, smmlv, tipoTope, afiliadoCaja, viveEnBogota, cesantias, ahorroActual);

        var techo = CalcularTecho(ingresoMensual, totalSubsidios, disponibleParaInicial, topeVis, esVis || esVip);
        var umbral = RevisarUmbral(ingresoMensual, ingresoEnSmmlv, smmlv, afiliadoCaja, viveEnBogota, esVis || esVip);

        return new PlanVivienda_Resultado(
            ValorVivienda: valorVivienda,
            Clasificacion: esVip ? "VIP" : esVis ? "VIS" : "No VIS",
            TopeVis: topeVis,
            TopeVip: topeVip,
            IngresoEnSmmlv: Math.Round(ingresoEnSmmlv, 1),
            AnioEscrituracion: anioEscrituracion ?? DateTime.UtcNow.Year,
            Ayudas: ayudas,
            TotalSubsidiosSeguros: totalSubsidios,
            Opciones: opciones,
            AhorroActual: ahorroActual,
            Cesantias: cesantias,
            DisponibleParaCuotaInicial: disponibleParaInicial,
            SiFueraVis: comparacion,
            Techo: techo,
            Umbral: umbral,
            DatosVigentesDesde: ParametrosVivienda.VigenteDesde);
    }

    /// <summary>
    /// Arma el plan de la vivienda más cara que todavía sería VIS, para poder
    /// poner los dos lado a lado. Una No VIS no es "imposible", es otra
    /// decisión — y verla contra la alternativa es lo que permite tomarla.
    /// </summary>
    private static ComparacionVis CompararConLaMejorVis(
        decimal topeVis, decimal ingresoMensual, decimal smmlv, TipoTopeVis tipoTope,
        bool afiliadoCaja, bool viveEnBogota, decimal cesantias, decimal ahorroActual)
    {
        var plan = Calcular(topeVis, ingresoMensual, smmlv, tipoTope, afiliadoCaja, viveEnBogota, cesantias, ahorroActual);
        var mejor = plan.Opciones.OrderBy(o => o.CuotaMensual).First();

        return new ComparacionVis(
            ValorMaximoVis: topeVis,
            SubsidiosQuePerdieron: plan.TotalSubsidiosSeguros,
            CuotaMensual: mejor.CuotaMensual,
            IngresoMinimoRequerido: mejor.IngresoMinimoRequerido);
    }

    /// <summary>
    /// Hasta cuánto pueden comprar. Se calcula con la vía más favorable —el
    /// FNA en VIS, que financia el 100%— porque el techo que importa es el
    /// mejor alcanzable, no el promedio.
    /// </summary>
    private static TechoDeCompra CalcularTecho(
        decimal ingresoMensual, decimal subsidios, decimal disponibleParaInicial,
        decimal topeVis, bool esSocial)
    {
        var tasa = esSocial ? ParametrosVivienda.TasaFnaVis - ParametrosVivienda.CoberturaTasaVis
                            : ParametrosVivienda.TasaFnaNoVis;
        var plazo = esSocial ? ParametrosVivienda.PlazoMesesVis : ParametrosVivienda.PlazoMesesNoVis;
        var financia = esSocial ? ParametrosVivienda.FinanciacionFnaVis : ParametrosVivienda.FinanciacionBancoNoVis;

        var maximo = CalculadoraCapacidad.PrecioMaximo(
            ingresoMensual, tasa, plazo, subsidios, disponibleParaInicial, financia);

        // Pasarse del tope VIS no "compra más casa": cuesta los subsidios, y
        // la plata que entra por crédito nunca alcanza a reponerlos. Por eso
        // el techo útil se corta ahí y se dice por qué.
        var limitadoPorElTope = maximo > topeVis;

        return new TechoDeCompra(
            PrecioMaximo: limitadoPorElTope ? Math.Floor(topeVis) : maximo,
            LimitadoPorElTopeVis: limitadoPorElTope,
            CuotaEstimada: Math.Round(ingresoMensual * ParametrosVivienda.ProporcionMaximaDelIngreso));
    }

    /// <summary>
    /// Avisa cuando el ingreso está apenas por encima de un umbral que vale
    /// mucha plata.
    ///
    /// El salto de los 2 SMMLV es brutal: por debajo, la caja da 30 salarios
    /// mínimos y se le puede sumar Mi Casa Ya; por encima, la caja baja a 20 y
    /// la concurrencia desaparece. Son 30 salarios mínimos de diferencia por
    /// ganar un peso de más. Nadie puede tomar una buena decisión sin saber
    /// que ese escalón existe.
    /// </summary>
    private static AlertaUmbral? RevisarUmbral(
        decimal ingresoMensual, decimal ingresoEnSmmlv, decimal smmlv,
        bool afiliadoCaja, bool viveEnBogota, bool esSocial)
    {
        if (!esSocial || !afiliadoCaja) return null;
        if (ingresoEnSmmlv <= ParametrosVivienda.IngresoMaximoConcurrenciaSmmlv) return null;

        // Solo tiene sentido avisar si está cerca; a cuatro salarios mínimos
        // el dato es ruido.
        const decimal margenParaAvisar = 0.35m;
        var exceso = ingresoEnSmmlv - ParametrosVivienda.IngresoMaximoConcurrenciaSmmlv;
        if (exceso > margenParaAvisar) return null;

        var ingresoDelUmbral = ParametrosVivienda.IngresoMaximoConcurrenciaSmmlv * smmlv;

        var conConcurrencia = ParametrosVivienda.TopeConcurrenciaSmmlv
            + (viveEnBogota ? ParametrosVivienda.SubsidioDistritalBogotaMinimoSmmlv : 0m);
        var sinConcurrencia = ParametrosVivienda.SubsidioCajaHasta4Smmlv
            + (viveEnBogota ? ParametrosVivienda.SubsidioDistritalBogotaMinimoSmmlv : 0m);

        return new AlertaUmbral(
            IngresoDelUmbral: Math.Floor(ingresoDelUmbral),
            SeExcedenPor: Math.Ceiling(ingresoMensual - ingresoDelUmbral),
            SubsidiosSiEstuvieranDebajo: (conConcurrencia - sinConcurrencia) * smmlv);
    }

    private static List<AyudaDisponible> CalcularAyudas(
        bool esVis, bool esVip, decimal ingresoEnSmmlv, decimal smmlv, bool afiliadoCaja, bool viveEnBogota)
    {
        var ayudas = new List<AyudaDisponible>();

        if (!esVis && !esVip)
        {
            ayudas.Add(new AyudaDisponible(
                "Subsidios de vivienda", 0, false,
                "Esta vivienda pasa el tope VIS, así que no aplica a ningún subsidio. Bajando la búsqueda por debajo del tope se abren todas las ayudas de esta lista."));
            return ayudas;
        }

        var hayConcurrencia = ingresoEnSmmlv <= ParametrosVivienda.IngresoMaximoConcurrenciaSmmlv;

        // --- Caja de compensación ---
        var cajaSmmlv = ingresoEnSmmlv switch
        {
            <= 2m => ParametrosVivienda.SubsidioCajaHasta2Smmlv,
            <= 4m => ParametrosVivienda.SubsidioCajaHasta4Smmlv,
            _ => 0m
        };

        if (!afiliadoCaja)
        {
            ayudas.Add(new AyudaDisponible("Subsidio de caja de compensación", 0, false,
                "Si alguno de los dos cotiza a una caja, les corresponderían entre 20 y 30 salarios mínimos. Es la ayuda más grande y la puerta de entrada a las demás: sin ella no hay concurrencia."));
            cajaSmmlv = 0;
        }
        else if (cajaSmmlv > 0)
        {
            ayudas.Add(new AyudaDisponible("Subsidio de caja de compensación", cajaSmmlv * smmlv, true,
                $"Les corresponden {cajaSmmlv:N0} salarios mínimos por ganar {ingresoEnSmmlv:N1} SMMLV. Este va primero: hay que tenerlo aprobado y sin aplicar antes de pedir el del Gobierno."));
        }
        else
        {
            ayudas.Add(new AyudaDisponible("Subsidio de caja de compensación", 0, false,
                $"Con {ingresoEnSmmlv:N1} SMMLV quedan por encima del tope de 4 salarios mínimos."));
        }

        // --- Mi Casa Ya, en concurrencia ---
        if (hayConcurrencia && afiliadoCaja)
        {
            // El tope de la concurrencia es conjunto, así que lo que aporta Mi
            // Casa Ya es lo que quepa hasta 50 SMMLV contando ya la caja.
            var margen = Math.Max(0, ParametrosVivienda.TopeConcurrenciaSmmlv - cajaSmmlv);
            var miCasaYaSmmlv = Math.Min(margen, ParametrosVivienda.MiCasaYaSisbenBajoSmmlv);

            ayudas.Add(new AyudaDisponible("Mi Casa Ya (en concurrencia)", miCasaYaSmmlv * smmlv, true,
                $"Ganando hasta 2 salarios mínimos pueden sumarlo al de la caja, hasta {ParametrosVivienda.TopeConcurrenciaSmmlv:N0} SMMLV entre los dos. Piden el de la caja primero y con ese aprobado solicitan este. Necesitan Sisbén entre A1 y D20."));
        }
        else if (ingresoEnSmmlv <= 4m)
        {
            ayudas.Add(new AyudaDisponible("Mi Casa Ya", ParametrosVivienda.MiCasaYaSisbenAltoSmmlv * smmlv, false,
                "Quedan pocos cupos remanentes en 2026 y la concurrencia con la caja solo aplica hasta 2 salarios mínimos. No lo cuenten como seguro hasta que se lo confirmen."));
        }

        // --- Subsidio distrital ---
        if (viveEnBogota && ingresoEnSmmlv <= ParametrosVivienda.IngresoMaximoDistritalBogotaSmmlv)
        {
            // Se cuenta el piso del rango: que la cifra real sea mayor es una
            // buena sorpresa, al revés sería un problema.
            ayudas.Add(new AyudaDisponible(
                "Subsidio distrital de Bogotá", ParametrosVivienda.SubsidioDistritalBogotaMinimoSmmlv * smmlv, true,
                $"\"Mi Casa en Bogotá\" da entre {ParametrosVivienda.SubsidioDistritalBogotaMinimoSmmlv:N0} y {ParametrosVivienda.SubsidioDistritalBogotaMaximoSmmlv:N0} salarios mínimos a hogares de hasta 4 SMMLV. Acá contamos el mínimo; les puede tocar más. Se pide en la Secretaría del Hábitat."));
        }
        else if (!viveEnBogota)
        {
            ayudas.Add(new AyudaDisponible("Subsidio de su ciudad o departamento", 0, false,
                "Varias alcaldías y gobernaciones tienen su propio subsidio que se suma a los demás. Vale la pena preguntar en la secretaría de vivienda: en Bogotá, por ejemplo, son entre 10 y 30 salarios mínimos más."));
        }

        // --- Cobertura a la tasa ---
        var cobertura = esVip ? ParametrosVivienda.CoberturaTasaVip : ParametrosVivienda.CoberturaTasaVis;
        ayudas.Add(new AyudaDisponible("Cobertura a la tasa de interés", 0, false,
            $"El Gobierno cubre {cobertura * 100:N0} puntos de la tasa durante los primeros {ParametrosVivienda.MesesCoberturaTasa / 12} años. No es plata en mano: ya está descontada de la cuota que ven abajo."));

        return ayudas;
    }

    private static List<OpcionCredito> CalcularOpciones(
        decimal valorVivienda, decimal ingresoMensual, bool esVis, bool esVip,
        decimal ingresoEnSmmlv, decimal subsidios, decimal disponibleParaInicial, decimal smmlv)
    {
        var opciones = new List<OpcionCredito>();
        var esSocial = esVis || esVip;

        var plazo = esSocial ? ParametrosVivienda.PlazoMesesVis : ParametrosVivienda.PlazoMesesNoVis;
        var cobertura = esSocial
            ? (esVip ? ParametrosVivienda.CoberturaTasaVip : ParametrosVivienda.CoberturaTasaVis)
            : 0m;

        // Una No VIS no se queda sin nada: el FRECH No VIS son 42 SMMLV
        // repartidos en 84 meses contra los intereses, para primera vivienda
        // nueva de hasta 500 SMMLV. Es un monto fijo mensual, no un descuento
        // en la tasa, así que se resta de la cuota.
        var frechMensual = !esSocial && valorVivienda <= ParametrosVivienda.FrechNoVisTopeViviendaSmmlv * smmlv
            ? ParametrosVivienda.FrechNoVisTotalSmmlv * smmlv / ParametrosVivienda.MesesCoberturaTasa
            : 0m;

        if (esSocial)
        {
            var aplicaTasaSocial = ingresoEnSmmlv <= ParametrosVivienda.IngresoMaximoTasaSocialSmmlv;

            opciones.Add(Armar(
                "FNA",
                aplicaTasaSocial ? ParametrosVivienda.TasaFnaSocial : ParametrosVivienda.TasaFnaVis,
                // La Tasa Social ya es una tasa subsidiada; no se le encima la
                // cobertura del Gobierno. Sumar las dos daría una cuota que
                // nadie va a ver en la vida real.
                aplicaTasaSocial ? 0m : cobertura,
                valorVivienda, ParametrosVivienda.FinanciacionFnaVis, subsidios,
                ingresoMensual, disponibleParaInicial, plazo, 0m,
                aplicaTasaSocial
                    ? "Tasa Social del 7% para hogares de hasta 2 salarios mínimos, con cupos limitados. El FNA financia el 100% de la VIS a afiliados que compran su primera vivienda, así que puede no hacer falta cuota inicial."
                    : "El FNA financia el 100% de la vivienda VIS/VIP a afiliados que compran la primera, así que puede no hacer falta cuota inicial."));

            opciones.Add(Armar(
                "Banco", ParametrosVivienda.TasaBancoMinima, cobertura,
                valorVivienda, ParametrosVivienda.FinanciacionBancoVis, subsidios,
                ingresoMensual, disponibleParaInicial, plazo, 0m,
                $"Con la mejor tasa del mercado; el rango va de {ParametrosVivienda.TasaBancoMinima * 100:N1}% a {ParametrosVivienda.TasaBancoMaxima * 100:N1}%, así que cotizar en varios bancos cambia la cuota. Financian hasta el 80% de una VIS."));
        }
        else
        {
            opciones.Add(Armar(
                "FNA", ParametrosVivienda.TasaFnaNoVis, 0m,
                valorVivienda, ParametrosVivienda.FinanciacionBancoNoVis, subsidios,
                ingresoMensual, disponibleParaInicial, plazo, frechMensual,
                "Para vivienda No VIS el FNA suele tener la tasa más baja del mercado, pero financia hasta el 70%."));

            opciones.Add(Armar(
                "Banco", ParametrosVivienda.TasaBancoMinima, 0m,
                valorVivienda, ParametrosVivienda.FinanciacionBancoNoVis, subsidios,
                ingresoMensual, disponibleParaInicial, plazo, frechMensual,
                $"El rango del mercado va de {ParametrosVivienda.TasaBancoMinima * 100:N1}% a {ParametrosVivienda.TasaBancoMaxima * 100:N1}%."));
        }

        return opciones;
    }

    private static OpcionCredito Armar(
        string entidad, decimal tasaEa, decimal cobertura, decimal valorVivienda,
        decimal proporcionFinanciable, decimal subsidios, decimal ingresoMensual,
        decimal disponibleParaInicial, int plazoMeses, decimal frechMensual, string nota)
    {
        var maximoFinanciable = valorVivienda * proporcionFinanciable;
        var cuotaInicialNecesaria = Math.Max(0, valorVivienda - maximoFinanciable - subsidios);
        var aFinanciar = Math.Max(0, valorVivienda - subsidios - cuotaInicialNecesaria);

        var tasaConCobertura = Math.Max(0, tasaEa - cobertura);

        // Dos cuotas, y las dos se muestran. La cobertura dura 84 meses y
        // después la cuota sube: enseñar solo la barata sería mentir por
        // omisión justo en el número con el que alguien firma.
        var cuotaPlena = CuotaMensual(aFinanciar, tasaEa, plazoMeses);

        // El FRECH No VIS se resta como monto fijo; el de VIS bajó la tasa más
        // arriba. Nunca por debajo de cero: el subsidio cubre intereses, no
        // regala capital.
        var cuotaConCobertura = Math.Max(0, CuotaMensual(aFinanciar, tasaConCobertura, plazoMeses) - frechMensual);

        var proporcion = ingresoMensual > 0 ? cuotaConCobertura / ingresoMensual : 0;
        var proporcionPlena = ingresoMensual > 0 ? cuotaPlena / ingresoMensual : 0;

        return new OpcionCredito(
            Entidad: entidad,
            TasaEfectivaAnual: tasaEa,
            TasaConCobertura: tasaConCobertura,
            TieneCobertura: cobertura > 0,
            PlazoMeses: plazoMeses,
            MontoAFinanciar: Math.Round(aFinanciar),
            CuotaInicialNecesaria: Math.Round(cuotaInicialNecesaria),
            CuotaMensual: Math.Round(cuotaConCobertura),
            CuotaDespuesDeLaCobertura: Math.Round(cuotaPlena),
            // El número más accionable de todos: cuánto tendrían que ganar
            // entre los dos para que se lo aprueben. "No te alcanza" no dice
            // qué hacer; "les faltan ochocientos mil al mes" sí.
            IngresoMinimoRequerido: Math.Round(cuotaConCobertura / ParametrosVivienda.ProporcionMaximaDelIngreso),
            ProporcionDelIngreso: Math.Round(proporcion, 3),
            CabeEnElIngreso: proporcion <= ParametrosVivienda.ProporcionMaximaDelIngreso,
            CabeCuandoSubaLaCuota: proporcionPlena <= ParametrosVivienda.ProporcionMaximaDelIngreso,
            LeFaltaParaLaCuotaInicial: Math.Max(0, Math.Round(cuotaInicialNecesaria - disponibleParaInicial)),
            Nota: nota);
    }

    /// <summary>
    /// Cuota fija de un crédito con amortización francesa. La tasa llega
    /// efectiva anual —así se publica en Colombia— y hay que pasarla a
    /// mensual equivalente: dividir entre 12 daría una cuota más baja que la
    /// real, que es exactamente el error que no se puede cometer acá.
    /// </summary>
    public static decimal CuotaMensual(decimal capital, decimal tasaEfectivaAnual, int meses)
    {
        if (capital <= 0 || meses <= 0) return 0;

        var mensual = (decimal)(Math.Pow(1 + (double)tasaEfectivaAnual, 1.0 / 12) - 1);
        if (mensual <= 0) return capital / meses;

        var factor = (decimal)Math.Pow(1 + (double)mensual, meses);
        return capital * mensual * factor / (factor - 1);
    }
}

public record AyudaDisponible(string Nombre, decimal Monto, bool EsSeguro, string Detalle);

public record OpcionCredito(
    string Entidad,
    decimal TasaEfectivaAnual,
    decimal TasaConCobertura,
    bool TieneCobertura,
    int PlazoMeses,
    decimal MontoAFinanciar,
    decimal CuotaInicialNecesaria,
    decimal CuotaMensual,
    decimal CuotaDespuesDeLaCobertura,
    decimal IngresoMinimoRequerido,
    decimal ProporcionDelIngreso,
    bool CabeEnElIngreso,
    bool CabeCuandoSubaLaCuota,
    decimal LeFaltaParaLaCuotaInicial,
    string Nota);

public record PlanVivienda_Resultado(
    decimal ValorVivienda,
    string Clasificacion,
    decimal TopeVis,
    decimal TopeVip,
    decimal IngresoEnSmmlv,
    int AnioEscrituracion,
    List<AyudaDisponible> Ayudas,
    decimal TotalSubsidiosSeguros,
    List<OpcionCredito> Opciones,
    decimal AhorroActual,
    decimal Cesantias,
    decimal DisponibleParaCuotaInicial,
    ComparacionVis? SiFueraVis,
    TechoDeCompra Techo,
    AlertaUmbral? Umbral,
    DateOnly DatosVigentesDesde);

public record TechoDeCompra(
    decimal PrecioMaximo,
    bool LimitadoPorElTopeVis,
    decimal CuotaEstimada);

/// <param name="SubsidiosSiEstuvieranDebajo">
/// Cuánto más recibirían si el ingreso del hogar quedara bajo el umbral. Es
/// la cifra que convierte un escalón invisible de la norma en algo que se
/// puede tener en cuenta al decidir.
/// </param>
public record AlertaUmbral(
    decimal IngresoDelUmbral,
    decimal SeExcedenPor,
    decimal SubsidiosSiEstuvieranDebajo);

/// <param name="SubsidiosQuePerdieron">
/// Cuánto dejan sobre la mesa por elegir una vivienda que pasa el tope. Es la
/// cifra que de verdad decide en qué proyectos vale la pena mirar.
/// </param>
public record ComparacionVis(
    decimal ValorMaximoVis,
    decimal SubsidiosQuePerdieron,
    decimal CuotaMensual,
    decimal IngresoMinimoRequerido);
