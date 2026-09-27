using System.Globalization;
using System.Text;

namespace MoneyBack.Api.Services;

/// <summary>
/// Un ícono tiene que ser un emoji y nada más.
///
/// El campo aceptaba cualquier texto, así que se podía guardar una frase
/// entera como "ícono" de una categoría. No rompe la base, pero sí la
/// pantalla: donde debía ir un símbolo de 18px aparece un párrafo, y la
/// lista de categorías queda ilegible. Y como el mismo campo se muestra en
/// el atajo y en las notificaciones, el problema se riega.
///
/// Se valida en el servidor y no solo en la pantalla porque la pantalla no
/// es la única puerta: el API se puede llamar directo.
/// </summary>
public static class Emoji
{
    /// <summary>
    /// Se queda con el primer símbolo si lo que llega es válido; si no,
    /// devuelve null para que quien llama decida el valor por defecto.
    ///
    /// Un emoji puede ocupar varios caracteres —🇨🇴 son dos, 👩‍👩‍👧 son cinco
    /// con uniones invisibles— así que se toma el primer "elemento de texto"
    /// completo y no el primer char, que partiría el símbolo a la mitad.
    /// </summary>
    public static string? Primero(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;

        var enumerador = StringInfo.GetTextElementEnumerator(valor.Trim());
        if (!enumerador.MoveNext()) return null;

        var primero = (string)enumerador.Current;
        return EsEmoji(primero) ? primero : null;
    }

    private static bool EsEmoji(string elemento)
    {
        if (elemento.Length == 0) return false;

        // Letras, dígitos, espacios y puntuación quedan fuera: eso es lo que
        // deja pasar una frase escrita por accidente.
        foreach (var rune in elemento.EnumerateRunes())
        {
            if (Rune.IsLetter(rune) || Rune.IsDigit(rune) || Rune.IsWhiteSpace(rune)) return false;
            if (Rune.IsPunctuation(rune) || Rune.IsSeparator(rune)) return false;
        }

        // Al menos una parte tiene que estar en los rangos de emoji o ser un
        // símbolo. Así "©" o un acento suelto tampoco pasan por ícono.
        return elemento.EnumerateRunes().Any(r =>
            r.Value is >= 0x1F300 and <= 0x1FAFF   // pictogramas, caras, objetos, banderas
                or >= 0x2600 and <= 0x27BF          // símbolos misceláneos y dingbats
                or >= 0x1F000 and <= 0x1F2FF        // fichas, cartas, símbolos encerrados
                or 0x200D or 0xFE0F                 // unión y selector de variante
                or >= 0x1F1E6 and <= 0x1F1FF);      // letras regionales (banderas)
    }
}
