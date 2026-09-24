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
    /// <param name="categoriaPorDefecto">
    /// Nombre de la categoría a usar cuando el texto no menciona ninguna. Lo
    /// usa el atajo de SMS: el banco escribe "EXITO" o "RAPPI", no "Mercado",
    /// así que la categoría la elige la persona al configurar el atajo. Se
    /// ignora si el texto sí nombra una categoría (que es el caso del atajo
    /// del Botón de Acción, donde la persona escribe "15000 mercado").
    /// </param>
    /// <param name="categoriaForzada">
    /// Gana sobre lo que diga el texto. Lo usa el atajo de SMS: el nombre del
    /// comercio puede chocar por casualidad con el de una categoría ("MERCADO
    /// LIBRE" caía en Mercado aunque no sea mercado), y que el destino dependa
    /// de esa coincidencia es impredecible. Si la persona configuró una
    /// categoría para sus SMS, esa manda.
    /// </param>
    /// <param name="vieneDeCaptura">
    /// El texto salió de leer una imagen, no de un mensaje. Una captura del
    /// centro de notificaciones trae varias transacciones apiladas, y quedarse
    /// con la primera registraría la compra equivocada sin avisar. En un SMS
    /// eso no pasa: trae un solo movimiento, y si menciona el saldo va después
    /// del monto, así que el primero es el correcto por convención.
    /// </param>
    public static ResultadoInterpretacion Interpretar(
        string? texto, IReadOnlyList<Categoria> categorias, string? categoriaPorDefecto = null,
        string? categoriaForzada = null, bool vieneDeCaptura = false)
    {
        if (string.IsNullOrWhiteSpace(texto)) return ResultadoInterpretacion.Fallo("No llegó ningún texto.");
        if (categorias.Count == 0) return ResultadoInterpretacion.Fallo("No tienes categorías activas todavía.");

        var detectado = DetectarMonto(texto);
        if (detectado is null || detectado.Valor <= 0)
        {
            return ResultadoInterpretacion.Fallo($"No encontré un monto en \"{Recortar(texto)}\". Regístralo a mano en MoneyBack.");
        }

        // Acá está la defensa contra bancos con formatos que nadie ha visto.
        // No se puede garantizar entender a todos; lo que sí se puede es no
        // inventar. Si el número no venía marcado como plata (sin "$", sin
        // "por", sin separador de miles) y en el texto hay varios números
        // —fechas, teléfonos, direcciones como "CALLE 100"— entonces elegir
        // uno sería adivinar. Mejor negarse: un gasto sin registrar se nota
        // y se corrige; uno registrado con el monto equivocado aparece
        // semanas después cuadrando cuentas.
        if (!detectado.ConAncla && ContarNumeros(texto) > 1)
        {
            return ResultadoInterpretacion.Fallo(
                $"No estoy seguro de cuál es el monto en \"{Recortar(texto)}\". Regístralo a mano en MoneyBack.");
        }

        if (vieneDeCaptura && RegexMontoConSimbolo().Matches(texto).Count > 1)
        {
            return ResultadoInterpretacion.Fallo(
                "La captura tiene varias notificaciones y no sé cuál registrar. Recórtala para que quede una sola y compártela otra vez.");
        }

        var monto = (decimal?)detectado.Valor;

        if (!string.IsNullOrWhiteSpace(categoriaForzada))
        {
            var forzada = PorNombre(categoriaForzada, categorias);
            return forzada is null
                ? ResultadoInterpretacion.Fallo($"No tienes una categoría llamada \"{categoriaForzada}\".")
                : new ResultadoInterpretacion(monto.Value, forzada, null);
        }

        var categoria = BuscarCategoria(texto, categorias);

        if (categoria is null && !string.IsNullOrWhiteSpace(categoriaPorDefecto))
        {
            categoria = PorNombre(categoriaPorDefecto, categorias);

            if (categoria is null)
            {
                return ResultadoInterpretacion.Fallo($"No tienes una categoría llamada \"{categoriaPorDefecto}\".");
            }
        }

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
    /// <param name="ConAncla">
    /// true si el número venía marcado como plata — con "$", después de "por",
    /// o con separador de miles. false significa que era el único número
    /// suelto del texto y se asumió que era el monto.
    /// </param>
    public record MontoDetectado(decimal Valor, bool ConAncla);

    public static decimal? ExtraerMonto(string texto) => DetectarMonto(texto)?.Valor;

    public static MontoDetectado? DetectarMonto(string texto)
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

        // El orden importa. Los nombres de comercio traen números ("OXXO CALLE
        // 100", "PRESTO ISERRA 100") y los SMS traen fechas y teléfonos, así
        // que agarrar "el primer número" se equivoca feo: en "en OXXO CALLE
        // 100 por 950" sacaba 100 en vez de 950. Primero se buscan las señales
        // de que un número ES el monto — el signo $ o la palabra "por", que es
        // como lo escriben los bancos colombianos — y solo si no hay ninguna
        // se cae al número suelto.
        var conAncla = true;
        var crudo =
            Capturar(RegexMontoConSimbolo(), limpio)
            ?? Capturar(RegexMontoDespuesDePor(), limpio)
            ?? Capturar(RegexNumeroConSeparadores(), limpio);

        if (crudo is null)
        {
            // Sin ninguna señal de que sea plata: es una suposición.
            conAncla = false;
            crudo = Capturar(RegexNumeroSimple(), limpio);
        }

        if (crudo is null) return null;
        crudo = crudo.Replace(".", "").Replace(",", "");
        if (!decimal.TryParse(crudo, NumberStyles.Integer, CultureInfo.InvariantCulture, out var valor)) return null;

        return new MontoDetectado(valor * multiplicador, conAncla || multiplicador > 1);
    }

    /// <summary>
    /// Cuántos números distintos aparecen. Sirve para saber si una suposición
    /// es segura: con un solo número no hay de dónde equivocarse, con varios sí.
    /// </summary>
    private static int ContarNumeros(string texto) => RegexNumeroSimple().Matches(texto).Count;

    /// <summary>Un SMS completo no cabe en una notificación; se recorta para que el aviso siga siendo legible.</summary>
    private static string Recortar(string texto)
    {
        var limpio = texto.Trim();
        return limpio.Length <= 45 ? limpio : limpio[..45] + "...";
    }

    /// <summary>
    /// Saca el nombre del comercio para guardarlo como nota del movimiento.
    /// Sin esto, la lista del día a día muestra "Otros gastos / Otros gastos"
    /// y toca abrir la app del banco para recordar en qué se gastó.
    ///
    /// Son dos formatos distintos y ninguno es adivinanza:
    ///   Davivienda: "...transaccion en PRESTO ISERRA 100 por 33,800 con tu..."
    ///   Nu:         "Tostao Coffee and Bread. Bogotá, Bogotá\n$ 6.600"
    /// Si el texto no calza con ninguno, devuelve null — mejor sin nota que
    /// con un pedazo de frase que no significa nada.
    /// </summary>
    public static string? ExtraerComercio(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;

        var entrePor = RegexComercioEntreEnYPor().Match(texto);
        if (entrePor.Success) return Limpiar(entrePor.Groups[1].Value);

        // Nu: el comercio abre el mensaje y la ciudad va después del punto.
        // Se exige que el texto traiga varias líneas porque así llega el
        // contenido de una notificación; lo que alguien escribe a mano
        // ("15000 mercado") viene en una sola y no tiene comercio que sacar.
        var lineas = texto.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lineas.Length < 2) return null;

        foreach (var linea in lineas)
        {
            var candidato = linea.TrimStart();
            // Se saltan la línea del monto y las que empiezan por número,
            // que nunca son el nombre de un comercio.
            if (candidato.StartsWith('$') || (candidato.Length > 0 && char.IsDigit(candidato[0]))) continue;

            var antesDeCiudad = candidato.Split('.', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            var limpio = Limpiar(antesDeCiudad ?? "");

            // "Nu" o "DAVIbank" solos son el banco, no el comercio.
            if (limpio.Length > 3 && !limpio.Contains('$')) return limpio;
        }

        return null;
    }

    private static string Limpiar(string valor)
    {
        var limpio = valor.Trim().Trim('.', ',', ':', '-').Trim();
        return limpio.Length > 40 ? limpio[..40].Trim() : limpio;
    }

    private static Categoria? PorNombre(string nombre, IReadOnlyList<Categoria> categorias)
    {
        var buscado = Normalizar(nombre);
        return categorias.FirstOrDefault(c => Normalizar(c.Nombre) == buscado);
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

    /// <summary>Devuelve el grupo de captura si hay match, o el match completo si el patrón no tiene grupos.</summary>
    private static string? Capturar(Regex patron, string texto)
    {
        var m = patron.Match(texto);
        if (!m.Success) return null;
        return m.Groups.Count > 1 && m.Groups[1].Success ? m.Groups[1].Value : m.Value;
    }

    [GeneratedRegex(@"(\d+)\s*(?:mil|k|lucas)\b", RegexOptions.IgnoreCase)]
    private static partial Regex RegexMiles();

    [GeneratedRegex(@"\$\s*(\d{1,3}(?:[.,]\d{3})*|\d+)")]
    private static partial Regex RegexMontoConSimbolo();

    /// <summary>"por 33,800", "por 950" — el patrón que usan los bancos colombianos.</summary>
    [GeneratedRegex(@"\bpor\s+\$?\s*(\d{1,3}(?:[.,]\d{3})*|\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex RegexMontoDespuesDePor();

    /// <summary>"transaccion en PRESTO ISERRA 100 por 33,800" -> "PRESTO ISERRA 100".</summary>
    [GeneratedRegex(@"\ben\s+(.+?)\s+por\s+\$?\s*\d", RegexOptions.IgnoreCase)]
    private static partial Regex RegexComercioEntreEnYPor();

    [GeneratedRegex(@"\d{1,3}(?:[.,]\d{3})+")]
    private static partial Regex RegexNumeroConSeparadores();

    [GeneratedRegex(@"\d+")]
    private static partial Regex RegexNumeroSimple();
}
