using MoneyBack.Api.Models.Subsidios;

namespace MoneyBack.Api.Domain.Subsidios;

/// <summary>
/// Responde "¿me alcanza para esta vivienda, y cómo la pago?".
///
/// Reemplaza al simulador anterior, que le pedía a la persona el monto de su
/// subsidio y se lo devolvía sumado — es decir, le pedía justo la respuesta
/// que debía darle. Acá lo único que no se puede saber es cuánto vale la casa
/// que está mirando y cuánto gana el hogar; todo lo demás se calcula.
///
/// La respuesta no es un número, son opciones: cada vía de financiación con su
/// cuota mensual y si esa cuota cabe en el ingreso. Eso es lo que cambia la
/// decisión de qué apartamento ir a ver.
/// </summary>
public static class PlanVivienda
{
    public static PlanVivienda_Resultado Calcular(
        decimal valorVivienda,
        decimal ingresoMensual,
        decimal smmlv,
        TipoTopeVis tipoTope,
        bool afiliadoCaja,
        decimal ahorroActual)
    {
        var topeVis = TopesVis.TopeEnPesos(tipoTope, smmlv);
        var topeVip = TopesVis.TopeEnPesos(TipoTopeVis.Vip, smmlv);

        var esVip = valorVivienda <= topeVip;
        var esVis = valorVivienda <= topeVis;
        var ingresoEnSmmlv = smmlv > 0 ? ingresoMensual / smmlv : 0;

        var subsidios = CalcularSubsidios(esVis, ingresoEnSmmlv, smmlv, afiliadoCaja);
        var totalSubsidios = subsidios.Where(s => s.EsSeguro).Sum(s => s.Monto);

        var opciones = CalcularOpciones(valorVivienda, ingresoMensual, esVis, esVip, ingresoEnSmmlv, totalSubsidios, ahorroActual);

        return new PlanVivienda_Resultado(
            ValorVivienda: valorVivienda,
            Clasificacion: esVip ? "VIP" : esVis ? "VIS" : "No VIS",
            TopeVis: topeVis,
            TopeVip: topeVip,
            IngresoEnSmmlv: Math.Round(ingresoEnSmmlv, 1),
            Subsidios: subsidios,
            TotalSubsidiosSeguros: totalSubsidios,
            Opciones: opciones,
            AhorroActual: ahorroActual,
            DatosVigentesDesde: ParametrosVivienda.VigenteDesde);
    }

    private static List<AyudaDisponible> CalcularSubsidios(
        bool esVis, decimal ingresoEnSmmlv, decimal smmlv, bool afiliadoCaja)
    {
        var ayudas = new List<AyudaDisponible>();

        if (!esVis)
        {
            // Los subsidios de vivienda son para VIS/VIP. Decirlo explícito
            // vale más que dejar la lista vacía: quien ve una lista vacía no
            // sabe si es que no aplica o si la app no supo calcular.
            ayudas.Add(new AyudaDisponible(
                "Subsidios de vivienda", 0, false,
                "Esta vivienda está por encima del tope VIS, así que no aplica a ningún subsidio de vivienda. Si bajaran la búsqueda por debajo del tope, sí aplicarían."));
            return ayudas;
        }

        if (afiliadoCaja)
        {
            var enSmmlv = ingresoEnSmmlv switch
            {
                <= 2m => ParametrosVivienda.SubsidioCajaHasta2Smmlv,
                <= 4m => ParametrosVivienda.SubsidioCajaHasta4Smmlv,
                _ => 0m
            };

            ayudas.Add(enSmmlv > 0
                ? new AyudaDisponible("Subsidio de caja de compensación", enSmmlv * smmlv, true,
                    $"Les corresponden {enSmmlv:N0} salarios mínimos por ganar {ingresoEnSmmlv:N1} SMMLV. Hoy es la vía más realista: se pide en el portal de su caja.")
                : new AyudaDisponible("Subsidio de caja de compensación", 0, false,
                    $"Con {ingresoEnSmmlv:N1} SMMLV de ingreso quedan por encima del tope de 4 salarios mínimos."));
        }
        else
        {
            ayudas.Add(new AyudaDisponible("Subsidio de caja de compensación", 0, false,
                "Si alguno de los dos cotiza a una caja de compensación, podrían pedir entre 20 y 30 salarios mínimos. Vale la pena averiguarlo: es la ayuda más grande a la que se puede aspirar hoy."));
        }

        // Mi Casa Ya se informa pero no se suma: en 2026 quedan cupos
        // remanentes y contarlos como plata segura llevaría a alguien a
        // comprometerse con una cuota inicial que no va a poder pagar.
        var miCasaYa = ingresoEnSmmlv <= 4m
            ? ParametrosVivienda.MiCasaYaSisbenBajoSmmlv
            : 0m;

        ayudas.Add(miCasaYa > 0
            ? new AyudaDisponible("Mi Casa Ya", miCasaYa * smmlv, false,
                $"Hasta {miCasaYa:N0} salarios mínimos según su grupo del Sisbén, pero en 2026 quedan muy pocos cupos. No lo cuenten como seguro hasta que se lo confirmen.")
            : new AyudaDisponible("Mi Casa Ya", 0, false,
                "El programa apunta a hogares de hasta 4 salarios mínimos."));

        var cobertura = esVis ? ParametrosVivienda.CoberturaTasaVis : ParametrosVivienda.CoberturaTasaVip;
        ayudas.Add(new AyudaDisponible("Cobertura a la tasa de interés", 0, false,
            $"El Gobierno puede cubrir {cobertura * 100:N0} puntos de la tasa durante los primeros {ParametrosVivienda.MesesCoberturaTasa / 12} años. No es plata en mano: baja la cuota."));

        return ayudas;
    }

    private static List<OpcionCredito> CalcularOpciones(
        decimal valorVivienda, decimal ingresoMensual, bool esVis, bool esVip,
        decimal ingresoEnSmmlv, decimal subsidios, decimal ahorroActual)
    {
        var opciones = new List<OpcionCredito>();

        if (esVis || esVip)
        {
            // El FNA financia el 100% de VIS y VIP a afiliados que compran su
            // primera vivienda, así que acá la cuota inicial puede ser cero.
            var aplicaTasaSocial = ingresoEnSmmlv <= ParametrosVivienda.IngresoMaximoTasaSocialSmmlv;

            opciones.Add(Armar(
                "FNA",
                aplicaTasaSocial ? ParametrosVivienda.TasaFnaSocial : ParametrosVivienda.TasaFnaVis,
                valorVivienda, ParametrosVivienda.FinanciacionFnaVis, subsidios, ingresoMensual, ahorroActual,
                aplicaTasaSocial
                    ? "Tasa Social del 7%: aplica a hogares de hasta 2 salarios mínimos que compren VIS o VIP, con cupos limitados. Hay que afiliarse al FNA."
                    : "El FNA financia el 100% de la vivienda VIS/VIP a afiliados que compran la primera, así que puede no hacer falta cuota inicial."));

            opciones.Add(Armar(
                "Banco",
                ParametrosVivienda.TasaBancoMinima,
                valorVivienda, ParametrosVivienda.FinanciacionBancoVis, subsidios, ingresoMensual, ahorroActual,
                $"Con la mejor tasa del mercado. El rango va de {ParametrosVivienda.TasaBancoMinima * 100:N1}% a {ParametrosVivienda.TasaBancoMaxima * 100:N1}%, así que vale la pena cotizar en varios."));
        }
        else
        {
            opciones.Add(Armar(
                "FNA",
                ParametrosVivienda.TasaFnaNoVis,
                valorVivienda, ParametrosVivienda.FinanciacionBancoNoVis, subsidios, ingresoMensual, ahorroActual,
                "Para vivienda No VIS el FNA suele tener la tasa más baja del mercado, pero financia hasta el 70%."));

            opciones.Add(Armar(
                "Banco",
                ParametrosVivienda.TasaBancoMinima,
                valorVivienda, ParametrosVivienda.FinanciacionBancoNoVis, subsidios, ingresoMensual, ahorroActual,
                $"Con la mejor tasa del mercado. El rango va de {ParametrosVivienda.TasaBancoMinima * 100:N1}% a {ParametrosVivienda.TasaBancoMaxima * 100:N1}%."));
        }

        return opciones;
    }

    private static OpcionCredito Armar(
        string entidad, decimal tasaEa, decimal valorVivienda, decimal proporcionFinanciable,
        decimal subsidios, decimal ingresoMensual, decimal ahorroActual, string nota)
    {
        var maximoFinanciable = valorVivienda * proporcionFinanciable;

        // Los subsidios entran como cuota inicial, así que reducen lo que hay
        // que pedir prestado antes que lo que hay que tener ahorrado.
        var cuotaInicialNecesaria = Math.Max(0, valorVivienda - maximoFinanciable - subsidios);
        var aFinanciar = valorVivienda - subsidios - cuotaInicialNecesaria;

        var cuotaMensual = CuotaMensual(aFinanciar, tasaEa, ParametrosVivienda.PlazoMesesTipico);
        var proporcionDelIngreso = ingresoMensual > 0 ? cuotaMensual / ingresoMensual : 0;

        return new OpcionCredito(
            Entidad: entidad,
            TasaEfectivaAnual: tasaEa,
            MontoAFinanciar: Math.Round(aFinanciar),
            CuotaInicialNecesaria: Math.Round(cuotaInicialNecesaria),
            CuotaMensual: Math.Round(cuotaMensual),
            ProporcionDelIngreso: Math.Round(proporcionDelIngreso, 3),
            CabeEnElIngreso: proporcionDelIngreso <= ParametrosVivienda.ProporcionMaximaDelIngreso,
            LeFaltaParaLaCuotaInicial: Math.Max(0, Math.Round(cuotaInicialNecesaria - ahorroActual)),
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
    decimal MontoAFinanciar,
    decimal CuotaInicialNecesaria,
    decimal CuotaMensual,
    decimal ProporcionDelIngreso,
    bool CabeEnElIngreso,
    decimal LeFaltaParaLaCuotaInicial,
    string Nota);

public record PlanVivienda_Resultado(
    decimal ValorVivienda,
    string Clasificacion,
    decimal TopeVis,
    decimal TopeVip,
    decimal IngresoEnSmmlv,
    List<AyudaDisponible> Subsidios,
    decimal TotalSubsidiosSeguros,
    List<OpcionCredito> Opciones,
    decimal AhorroActual,
    DateOnly DatosVigentesDesde);
