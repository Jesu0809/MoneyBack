using System.Globalization;
using System.Text;

namespace MoneyBack.Web.Services;

/// <summary>
/// Formatea a mano (separador de miles con punto, como en Colombia) en vez de
/// depender de CultureInfo("es-CO"): Blazor WASM no siempre trae datos ICU
/// completos y una cultura no soportada lanza excepción en tiempo de ejecución.
/// </summary>
public static class Formato
{
    public static string Pesos(decimal valor)
    {
        var redondeado = Math.Round(valor, 0, MidpointRounding.AwayFromZero);
        var negativo = redondeado < 0;
        var digitos = Math.Abs(redondeado).ToString("F0", CultureInfo.InvariantCulture);

        var sb = new StringBuilder();
        for (var i = 0; i < digitos.Length; i++)
        {
            if (i > 0 && (digitos.Length - i) % 3 == 0) sb.Append('.');
            sb.Append(digitos[i]);
        }

        return (negativo ? "-$" : "$") + sb;
    }

    public static string Porcentaje(decimal valor) => valor.ToString("0.#", CultureInfo.InvariantCulture) + "%";

    private static readonly string[] Meses =
        ["ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sep", "oct", "nov", "dic"];

    public static string FechaCorta(DateTime fecha)
    {
        var local = fecha.ToLocalTime();
        return $"{local.Day} {Meses[local.Month - 1]} {local.Year}";
    }

    public static string FechaHora(DateTime fecha)
    {
        var local = fecha.ToLocalTime();
        var hora12 = local.Hour % 12 == 0 ? 12 : local.Hour % 12;
        var ampm = local.Hour < 12 ? "a.m." : "p.m.";
        return $"{local.Day} {Meses[local.Month - 1]}, {hora12}:{local.Minute:D2} {ampm}";
    }
}
