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
        decimal ahorroActual)
    {
        var topeVis = TopesVis.TopeEnPesos(tipoTope, smmlv);
        var topeVip = TopesVis.TopeEnPesos(TipoTopeVis.Vip, smmlv);

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
            valorVivienda, ingresoMensual, esVis, esVip, ingresoEnSmmlv, totalSubsidios, disponibleParaInicial);

        return new PlanVivienda_Resultado(
            ValorVivienda: valorVivienda,
            Clasificacion: esVip ? "VIP" : esVis ? "VIS" : "No VIS",
            TopeVis: topeVis,
            TopeVip: topeVip,
            IngresoEnSmmlv: Math.Round(ingresoEnSmmlv, 1),
            Ayudas: ayudas,
            TotalSubsidiosSeguros: totalSubsidios,
            Opciones: opciones,
            AhorroActual: ahorroActual,
            Cesantias: cesantias,
            DisponibleParaCuotaInicial: disponibleParaInicial,
            DatosVigentesDesde: ParametrosVivienda.VigenteDesde);
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
        decimal ingresoEnSmmlv, decimal subsidios, decimal disponibleParaInicial)
    {
        var opciones = new List<OpcionCredito>();
        var esSocial = esVis || esVip;

        var plazo = esSocial ? ParametrosVivienda.PlazoMesesVis : ParametrosVivienda.PlazoMesesNoVis;
        var cobertura = esSocial
            ? (esVip ? ParametrosVivienda.CoberturaTasaVip : ParametrosVivienda.CoberturaTasaVis)
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
                ingresoMensual, disponibleParaInicial, plazo,
                aplicaTasaSocial
                    ? "Tasa Social del 7% para hogares de hasta 2 salarios mínimos, con cupos limitados. El FNA financia el 100% de la VIS a afiliados que compran su primera vivienda, así que puede no hacer falta cuota inicial."
                    : "El FNA financia el 100% de la vivienda VIS/VIP a afiliados que compran la primera, así que puede no hacer falta cuota inicial."));

            opciones.Add(Armar(
                "Banco", ParametrosVivienda.TasaBancoMinima, cobertura,
                valorVivienda, ParametrosVivienda.FinanciacionBancoVis, subsidios,
                ingresoMensual, disponibleParaInicial, plazo,
                $"Con la mejor tasa del mercado; el rango va de {ParametrosVivienda.TasaBancoMinima * 100:N1}% a {ParametrosVivienda.TasaBancoMaxima * 100:N1}%, así que cotizar en varios bancos cambia la cuota. Financian hasta el 80% de una VIS."));
        }
        else
        {
            opciones.Add(Armar(
                "FNA", ParametrosVivienda.TasaFnaNoVis, 0m,
                valorVivienda, ParametrosVivienda.FinanciacionBancoNoVis, subsidios,
                ingresoMensual, disponibleParaInicial, plazo,
                "Para vivienda No VIS el FNA suele tener la tasa más baja del mercado, pero financia hasta el 70%."));

            opciones.Add(Armar(
                "Banco", ParametrosVivienda.TasaBancoMinima, 0m,
                valorVivienda, ParametrosVivienda.FinanciacionBancoNoVis, subsidios,
                ingresoMensual, disponibleParaInicial, plazo,
                $"El rango del mercado va de {ParametrosVivienda.TasaBancoMinima * 100:N1}% a {ParametrosVivienda.TasaBancoMaxima * 100:N1}%."));
        }

        return opciones;
    }

    private static OpcionCredito Armar(
        string entidad, decimal tasaEa, decimal cobertura, decimal valorVivienda,
        decimal proporcionFinanciable, decimal subsidios, decimal ingresoMensual,
        decimal disponibleParaInicial, int plazoMeses, string nota)
    {
        var maximoFinanciable = valorVivienda * proporcionFinanciable;
        var cuotaInicialNecesaria = Math.Max(0, valorVivienda - maximoFinanciable - subsidios);
        var aFinanciar = Math.Max(0, valorVivienda - subsidios - cuotaInicialNecesaria);

        var tasaConCobertura = Math.Max(0, tasaEa - cobertura);

        // Dos cuotas, y las dos se muestran. La cobertura dura 84 meses y
        // después la cuota sube: enseñar solo la barata sería mentir por
        // omisión justo en el número con el que alguien firma.
        var cuotaConCobertura = CuotaMensual(aFinanciar, tasaConCobertura, plazoMeses);
        var cuotaPlena = CuotaMensual(aFinanciar, tasaEa, plazoMeses);

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
    List<AyudaDisponible> Ayudas,
    decimal TotalSubsidiosSeguros,
    List<OpcionCredito> Opciones,
    decimal AhorroActual,
    decimal Cesantias,
    decimal DisponibleParaCuotaInicial,
    DateOnly DatosVigentesDesde);
