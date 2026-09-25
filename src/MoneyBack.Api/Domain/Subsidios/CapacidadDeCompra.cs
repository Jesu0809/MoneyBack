namespace MoneyBack.Api.Domain.Subsidios;

/// <summary>
/// La pregunta al revés: en vez de "¿me alcanza para esta vivienda?",
/// "¿hasta cuánto puedo?".
///
/// Es la que de verdad sirve para salir a buscar. Preguntando de a un
/// proyecto a la vez uno se entera de que no alcanza después de haberse
/// ilusionado; sabiendo el techo, uno filtra antes de mirar.
/// </summary>
public static class CalculadoraCapacidad
{
    /// <param name="proporcionFinanciable">
    /// Cuánto del valor presta la entidad. Con el FNA en VIS es 1.00 (financia
    /// todo), y entonces el único límite es cuánta cuota aguanta el ingreso.
    /// </param>
    public static decimal PrecioMaximo(
        decimal ingresoMensual, decimal tasaEfectivaAnual, int plazoMeses,
        decimal subsidios, decimal disponibleParaInicial, decimal proporcionFinanciable)
    {
        var cuotaMaxima = ingresoMensual * ParametrosVivienda.ProporcionMaximaDelIngreso;
        var creditoMaximo = CapitalQueAguanta(cuotaMaxima, tasaEfectivaAnual, plazoMeses);

        // Dos techos distintos, y manda el más bajo.
        //
        // Uno es cuánto aguanta el ingreso: más allá de eso no aprueban.
        var porElIngreso = subsidios + disponibleParaInicial + creditoMaximo;

        // El otro es cuánto exigen de cuota inicial: si el banco solo presta
        // el 80%, el 20% restante tiene que salir de subsidios y ahorro, y eso
        // limita el precio aunque el ingreso diera para más. Es el techo que
        // sorprende a la gente: le aprueban el crédito pero no tiene la
        // inicial.
        var porLaCuotaInicial = proporcionFinanciable >= 1m
            ? decimal.MaxValue
            : (subsidios + disponibleParaInicial) / (1m - proporcionFinanciable);

        return Math.Floor(Math.Min(porElIngreso, porLaCuotaInicial));
    }

    /// <summary>
    /// Cuánto capital corresponde a una cuota dada: la amortización francesa
    /// despejada al revés.
    /// </summary>
    public static decimal CapitalQueAguanta(decimal cuota, decimal tasaEfectivaAnual, int meses)
    {
        if (cuota <= 0 || meses <= 0) return 0;

        var mensual = (decimal)(Math.Pow(1 + (double)tasaEfectivaAnual, 1.0 / 12) - 1);
        if (mensual <= 0) return cuota * meses;

        var factor = (decimal)Math.Pow(1 + (double)mensual, meses);
        return cuota * (factor - 1) / (mensual * factor);
    }
}
