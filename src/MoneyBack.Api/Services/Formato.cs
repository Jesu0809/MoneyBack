using System.Globalization;

namespace MoneyBack.Api.Services;

/// <summary>
/// Formatea plata para textos que lee una persona: confirmaciones del atajo,
/// notificaciones push, resúmenes.
///
/// Vive acá porque el servidor corre en cultura invariante y ahí $15.000 sale
/// como "15,000" — formato gringo, que en Colombia se lee como quince con
/// cero. No es un detalle estético: un monto mal puntuado en una notificación
/// hace dudar de la cifra.
/// </summary>
public static class Formato
{
    private static readonly CultureInfo Colombia = CultureInfo.GetCultureInfo("es-CO");

    public static string Pesos(decimal monto) => $"${monto.ToString("N0", Colombia)}";

    /// <summary>Sin el signo, para cuando el texto ya lo trae o lo arma aparte.</summary>
    public static string Numero(decimal monto) => monto.ToString("N0", Colombia);
}
