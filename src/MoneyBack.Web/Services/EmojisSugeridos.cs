namespace MoneyBack.Web.Services;

/// <summary>
/// Propone íconos según lo que la persona escribió.
///
/// Una lista fija de ocho emojis obliga a que una meta de mascota sea un
/// blanco de tiro, y el teclado completo son cientos donde hay que buscar.
/// Lo que uno quiere casi siempre está implícito en el nombre: si escribió
/// "Lavadora", el ícono correcto es evidente y la app debería ofrecerlo sin
/// que se lo pidan.
/// </summary>
public static class EmojisSugeridos
{
    /// <summary>
    /// Palabras a íconos. Se compara sin tildes y en minúscula, y basta con
    /// que la palabra aparezca dentro del nombre: "viaje a Cartagena"
    /// encuentra "viaje".
    /// </summary>
    private static readonly (string[] Palabras, string Emoji)[] Pistas =
    [
        (["apartamento", "apto", "casa", "vivienda", "hogar", "cuota inicial"], "🏠"),
        (["emergencia", "imprevisto", "colchon", "respaldo"], "🛟"),
        (["lavadora", "nevera", "electrodomestico", "estufa", "secadora"], "🧺"),
        (["viaje", "vacaciones", "paseo", "vuelo", "tiquete"], "✈️"),
        (["carro", "moto", "vehiculo", "camioneta"], "🚗"),
        (["estudio", "matricula", "universidad", "curso", "semestre", "maestria"], "🎓"),
        (["boda", "matrimonio", "anillo", "grado"], "💍"),
        (["bebe", "hijo", "hija", "embarazo", "pañales"], "👶"),
        (["mascota", "perro", "gato", "veterinario"], "🐾"),
        (["computador", "portatil", "celular", "telefono", "tecnologia"], "💻"),
        (["mercado", "comida", "domicilio", "restaurante"], "🍔"),
        (["salud", "medico", "odontologo", "cirugia"], "🏥"),
        (["ropa", "vestuario", "zapatos"], "👕"),
        (["navidad", "regalo", "cumpleaños"], "🎁"),
        (["muebles", "sofa", "cama", "remodelacion"], "🛋️"),
        (["deuda", "credito", "prestamo", "tarjeta"], "💳"),
        (["gimnasio", "bicicleta", "deporte", "ejercicio"], "🚴"),
        (["negocio", "emprendimiento", "empresa"], "💼"),
    ];

    private static readonly string[] PorDefecto = ["🎯", "🏠", "🛟", "✈️", "🚗", "🎓", "🧺", "💰"];

    /// <summary>
    /// Los sugeridos van de primeros y sin repetir, seguidos de los
    /// genéricos hasta completar. Siempre se devuelve la misma cantidad para
    /// que la fila no cambie de tamaño mientras se escribe, que se siente
    /// como si la pantalla saltara.
    /// </summary>
    public static string[] Para(string? nombre, int cuantos = 8)
    {
        var elegidos = new List<string>();

        if (!string.IsNullOrWhiteSpace(nombre))
        {
            var limpio = Normalizar(nombre);

            foreach (var (palabras, emoji) in Pistas)
            {
                if (elegidos.Contains(emoji)) continue;
                if (palabras.Any(limpio.Contains)) elegidos.Add(emoji);
            }
        }

        foreach (var emoji in PorDefecto)
        {
            if (elegidos.Count >= cuantos) break;
            if (!elegidos.Contains(emoji)) elegidos.Add(emoji);
        }

        return [.. elegidos.Take(cuantos)];
    }

    /// <summary>
    /// Sin tildes y en minúscula: nadie escribe "matrícula" con tilde cuando
    /// va rápido, y "Viaje" con mayúscula debe encontrar lo mismo.
    ///
    /// Se reemplaza letra por letra en vez de usar String.Normalize, que en
    /// Blazor WebAssembly lanza PlatformNotSupportedException: el runtime del
    /// navegador no trae las tablas de normalización de Unicode. Escribir
    /// "Japón" tumbaba la pantalla entera con "An unhandled error has
    /// occurred", y el error solo aparecía al teclear una tilde.
    /// </summary>
    private static string Normalizar(string texto)
    {
        const string ConTilde = "áàäâãéèëêíìïîóòöôõúùüûñç";
        const string SinTilde = "aaaaaeeeeiiiiooooouuuunc";

        var construido = new System.Text.StringBuilder(texto.Length);

        foreach (var c in texto.ToLowerInvariant())
        {
            var i = ConTilde.IndexOf(c);
            construido.Append(i >= 0 ? SinTilde[i] : c);
        }

        return construido.ToString();
    }
}
