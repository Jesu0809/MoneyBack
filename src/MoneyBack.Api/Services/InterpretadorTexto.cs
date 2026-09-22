using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MoneyBack.Api.Models.DiaADia;

namespace MoneyBack.Api.Services;

/// <param name="Categoria">Null si no se pudo resolver; Razon explica por qué.</param>
public record ResultadoInterpretacion(decimal Monto, Categoria? Categoria, string? Razon)
{
    public bool Exito => Categoria is not null && Monto > 0;

    public static ResultadoInterpretacion Fallo(string razon) => new(0, null, razon);
}

/// <summary>
/// Convierte texto suelto en un movimiento: "15000 mercado", "$15.000 en
/// mercado", "merc 15 mil". Es el cerebro compartido de los dos atajos —
/// el del Botón de Acción (donde el usuario escribe) y, más adelante, el de
/// los SMS del banco (donde escribe el banco). Por eso vive aparte de los
/// endpoints y no asume de dónde viene el texto.
///
/// A propósito son reglas y no un modelo entrenado: el texto que llega es
/// corto y muy estructurado, las reglas aciertan alto desde el primer día,
/// se pueden depurar cuando fallan, y no cuestan nada por llamada. El
/// respaldo con LLM (ver InterpretadorLlm) solo entra cuando esto no logra
/// resolver, que es justo donde un modelo sí aporta.
/// </summary>
public static partial class InterpretadorTexto
{
    public static ResultadoInterpretacion Interpretar(string? texto, IReadOnlyList<Categoria> categorias)
    {
        if (string.IsNullOrWhiteSpace(texto)) return ResultadoInterpretacion.Fallo("No llegó ningún texto.");
        if (categorias.Count == 0) return ResultadoInterpretacion.Fallo("No tienes categorías activas todavía.");

        var monto = ExtraerMonto(texto);
        if (monto is null or <= 0)
        {
            return ResultadoInterpretacion.Fallo($"No encontré un monto en \"{texto.Trim()}\".");
        }

        var categoria = BuscarCategoria(texto, categorias);
        if (categoria is null)
        {
            var ejemplo = monto.Value.ToString("N0", CultureInfo.GetCultureInfo("es-CO"));
            return ResultadoInterpretacion.Fallo($"Entendí el monto pero no a qué categoría va. Escribe el nombre de una de tus categorías, por ejemplo \"{ejemplo} {categorias[0].Nombre.ToLowerInvariant()}\".");
        }

        return new ResultadoInterpretacion(monto.Value, categoria, null);
    }

    /// <summary>
    /// En Colombia el punto separa miles ("15.000" son quince mil, no quince).
    /// Se quitan los separadores solo cuando agrupan de a tres dígitos, que es
    /// lo que los distingue de un decimal real.
    /// </summary>
    public static decimal? ExtraerMonto(string texto)
    {
        var multiplicador = 1m;
        var limpio = texto;

        // "15 mil" / "15k" / "15 lucas" -> 15 * 1000
        var abreviado = RegexMiles().Match(limpio);
        if (abreviado.Success)
        {
            multiplicador = 1000m;
            limpio = limpio[..abreviado.Index] + " " + abreviado.Groups[1].Value + " ";
        }

        var conSeparadores = RegexNumeroConSeparadores().Match(limpio);
        var crudo = conSeparadores.Success
            ? conSeparadores.Value.Replace(".", "").Replace(",", "")
            : RegexNumeroSimple().Match(limpio) is { Success: true } simple ? simple.Value : null;

        if (crudo is null) return null;
        if (!decimal.TryParse(crudo, NumberStyles.Integer, CultureInfo.InvariantCulture, out var valor)) return null;

        return valor * multiplicador;
    }

    private static Categoria? BuscarCategoria(string texto, IReadOnlyList<Categoria> categorias)
    {
        var palabras = Normalizar(texto)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => !p.All(char.IsDigit))
            .ToList();

        if (palabras.Count == 0) return null;

        // Se recorre de nombre más largo a más corto para que "otros gastos"
        // le gane a "gastos" cuando ambas existan.
        foreach (var categoria in categorias.OrderByDescending(c => c.Nombre.Length))
        {
            var nombre = Normalizar(categoria.Nombre);
            if (palabras.Any(p => p == nombre)) return categoria;
        }

        foreach (var categoria in categorias.OrderByDescending(c => c.Nombre.Length))
        {
            var nombre = Normalizar(categoria.Nombre);
            // Prefijo de al menos 4 letras: "merc" encuentra "Mercado", pero
            // "ro" no dispara "Ropa" por accidente.
            if (palabras.Any(p => p.Length >= 4 && (nombre.StartsWith(p) || p.StartsWith(nombre)))) return categoria;
        }

        return null;
    }

    /// <summary>Minúsculas y sin tildes, para que "Salud" y "salúd" sean lo mismo.</summary>
    public static string Normalizar(string valor)
    {
        var descompuesto = valor.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sinTildes = new StringBuilder(descompuesto.Length);

        foreach (var caracter in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark)
            {
                sinTildes.Append(char.IsLetterOrDigit(caracter) || char.IsWhiteSpace(caracter) ? caracter : ' ');
            }
        }

        return sinTildes.ToString().Normalize(NormalizationForm.FormC);
    }

    [GeneratedRegex(@"(\d+)\s*(?:mil|k|lucas)\b", RegexOptions.IgnoreCase)]
    private static partial Regex RegexMiles();

    [GeneratedRegex(@"\d{1,3}(?:[.,]\d{3})+")]
    private static partial Regex RegexNumeroConSeparadores();

    [GeneratedRegex(@"\d+")]
    private static partial Regex RegexNumeroSimple();
}
