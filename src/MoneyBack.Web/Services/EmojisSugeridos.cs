namespace MoneyBack.Web.Services;

/// <summary>
/// Propone íconos a partir de lo que la persona escribió.
///
/// Busca contra las palabras en español de los 1.900 emojis de Unicode (ver
/// EmojisDatos), no contra una lista escrita a mano. La lista propia siempre
/// se quedaba corta de la forma más tonta: "perro" encontraba 🐶 y
/// "Colombia" no encontraba nada, y la única diferencia era que a mí se me
/// había ocurrido una palabra y no la otra.
/// </summary>
public static class EmojisSugeridos
{
    /// <summary>Los que se muestran cuando todavía no han escrito nada.</summary>
    private static readonly string[] PorDefecto = ["🎯", "🏠", "🛟", "✈️", "🚗", "🎓", "💰"];

    /// <summary>
    /// Palabras que aparecen en casi cualquier nombre y no dicen nada de qué
    /// ícono va. Sin esto, "viaje a Japón" buscaría también por "a".
    /// </summary>
    private static readonly HashSet<string> Vacías =
        ["para", "con", "del", "las", "los", "una", "uno", "por", "que", "nuestro", "nuestra", "mi", "de", "la", "el"];

    /// <summary>
    /// Lo que las anotaciones de Unicode no cubren para alguien en Colombia.
    ///
    /// El es.xml de CLDR está escrito en español de España y no trae
    /// banderas: "computador" no encuentra nada porque allá es "ordenador",
    /// "Colombia" tampoco, y "lavadora" no existe como emoji. Estas entradas
    /// van primero porque son las palabras con que la gente de verdad nombra
    /// una meta de ahorro.
    /// </summary>
    private static readonly Dictionary<string, string[]> Sinonimos = new()
    {
        ["ciudad"] = ["🏙️"], ["bogota"] = ["🏙️"], ["medellin"] = ["🏙️"], ["cali"] = ["🏙️"],
        ["cartagena"] = ["🏖️"], ["barranquilla"] = ["🏖️"], ["santamarta"] = ["🏖️"], ["playa"] = ["🏖️"],
        ["finca"] = ["🌄"], ["campo"] = ["🌄"], ["pueblo"] = ["🏘️"],
        ["gaseosa"] = ["🥤"], ["coca"] = ["🥤"], ["refresco"] = ["🥤"], ["jugo"] = ["🧃"], ["cerveza"] = ["🍺"], ["cafe"] = ["☕"], ["tinto"] = ["☕"],
        ["computador"] = ["💻"], ["portatil"] = ["💻"], ["celular"] = ["📱"],
        ["lavadora"] = ["🧺"], ["nevera"] = ["🧊"], ["estufa"] = ["🍳"], ["licuadora"] = ["🥤"],
        ["matricula"] = ["🎓"], ["semestre"] = ["🎓"], ["universidad"] = ["🎓"], ["estudio"] = ["📚"],
        ["gimnasio"] = ["🏋️"], ["gym"] = ["🏋️"],
        ["cuota"] = ["🏠"], ["inicial"] = ["🏠"], ["apartamento"] = ["🏠"], ["apto"] = ["🏠"],
        ["vivienda"] = ["🏠"], ["arriendo"] = ["🏠"], ["hogar"] = ["🏠"],
        ["emergencia"] = ["🛟"], ["imprevisto"] = ["🛟"], ["colchon"] = ["🛟"], ["ahorro"] = ["💰"],
        ["mercado"] = ["🛒"], ["domicilio"] = ["🛵"], ["comida"] = ["🍔"], ["almuerzo"] = ["🍽️"],
        ["carro"] = ["🚗"], ["moto"] = ["🏍️"], ["gasolina"] = ["⛽"], ["transporte"] = ["🚌"],
        ["viaje"] = ["✈️"], ["vacaciones"] = ["🏖️"], ["paseo"] = ["🧳"], ["tiquete"] = ["🎫"],
        ["salud"] = ["🏥"], ["medico"] = ["🩺"], ["droga"] = ["💊"], ["odontologo"] = ["🦷"],
        ["mascota"] = ["🐾"], ["veterinario"] = ["🐾"],
        ["ropa"] = ["👕"], ["zapatos"] = ["👟"], ["regalo"] = ["🎁"], ["navidad"] = ["🎄"],
        ["boda"] = ["💍"], ["matrimonio"] = ["💍"], ["bebe"] = ["👶"], ["grado"] = ["🎓"],
        ["deuda"] = ["💳"], ["tarjeta"] = ["💳"], ["credito"] = ["🏦"], ["banco"] = ["🏦"],
        ["negocio"] = ["💼"], ["trabajo"] = ["💼"], ["sueldo"] = ["💵"],
        ["muebles"] = ["🛋️"], ["cama"] = ["🛏️"], ["remodelacion"] = ["🔨"], ["arreglo"] = ["🔧"],
        ["internet"] = ["📶"], ["servicios"] = ["💡"], ["luz"] = ["💡"], ["agua"] = ["💧"],
        ["cumpleanos"] = ["🎂"], ["fiesta"] = ["🎉"], ["cine"] = ["🎬"], ["musica"] = ["🎵"],
    };

    private static readonly (string Emoji, string[] Palabras)[] Indice = ConstruirIndice();

    private static (string, string[])[] ConstruirIndice()
    {
        // Las banderas van primero: cuando alguien escribe el nombre de un
        // país quiere la bandera, no un emoji que por casualidad comparta
        // alguna letra.
        var todas = EmojisBanderas.Entradas.Concat(EmojisDatos.Entradas).ToArray();
        var indice = new (string, string[])[todas.Length];

        for (var i = 0; i < todas.Length; i++)
        {
            var corte = todas[i].IndexOf('|');
            indice[i] = (todas[i][..corte], todas[i][(corte + 1)..].Split(' '));
        }

        return indice;
    }

    /// <summary>
    /// Los que mejor coinciden, y se completa con genéricos para que la fila
    /// no cambie de tamaño mientras se escribe — que se siente como si la
    /// pantalla saltara.
    /// </summary>
    public static string[] Para(string? nombre, int cuantos = 7)
    {
        var terminos = Normalizar(nombre)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 3 && !Vacías.Contains(t))
            .ToArray();

        var elegidos = new List<string>();

        // Primero los sinónimos: son las palabras exactas con que la gente
        // nombra sus metas, y aciertan más que cualquier coincidencia
        // parcial contra las anotaciones de Unicode.
        foreach (var termino in terminos)
        {
            if (!Sinonimos.TryGetValue(termino, out var propios))
            {
                // "cuotainicial" debe encontrar lo mismo que "cuota".
                var clave = Sinonimos.Keys.FirstOrDefault(k =>
                    k.Length >= 4 && termino.Contains(k, StringComparison.Ordinal));
                if (clave is null) continue;
                propios = Sinonimos[clave];
            }

            foreach (var emoji in propios)
            {
                if (!elegidos.Contains(emoji)) elegidos.Add(emoji);
            }
        }

        if (elegidos.Count < cuantos && terminos.Length > 0)
        {
            // El rescate por palabra contenida solo corre si no hubo NADA:
            // ni sinónimo ni coincidencia directa. Si ya hay algo bueno,
            // añadirlo solo mete ruido — "Cartagena" traía naipes por
            // "carta" y "gaseosa" traía baños por "aseo".
            foreach (var emoji in Buscar(terminos, cuantos, permitirRescate: elegidos.Count == 0))
            {
                if (elegidos.Count >= cuantos) break;
                if (!elegidos.Contains(emoji)) elegidos.Add(emoji);
            }
        }

        foreach (var emoji in PorDefecto)
        {
            if (elegidos.Count >= cuantos) break;
            if (!elegidos.Contains(emoji)) elegidos.Add(emoji);
        }

        return [.. elegidos.Take(cuantos)];
    }

    private static List<string> Buscar(string[] terminos, int cuantos, bool permitirRescate)
    {
        var puntuados = new List<(string Emoji, int Puntos)>();

        foreach (var (emoji, palabras) in Indice)
        {
            var puntos = 0;

            foreach (var termino in terminos)
            {
                foreach (var palabra in palabras)
                {
                    // Igual vale mucho más que parecido: quien escribe "casa"
                    // quiere 🏠, no cualquier cosa que contenga esas letras.
                    if (palabra == termino) { puntos += 10; break; }
                    if (termino.Length >= 4 && palabra.StartsWith(termino, StringComparison.Ordinal)) { puntos += 5; break; }
                    if (termino.Length >= 5 && palabra.Contains(termino, StringComparison.Ordinal)) { puntos += 2; break; }
                }
            }

            if (puntos > 0) puntuados.Add((emoji, puntos));
        }

        // Solo si no hubo nada directo se intenta al revés: ver si alguna
        // palabra conocida está DENTRO de lo que escribieron. Sirve para
        // "cuotainicial", pero es ruidoso —"bogota" contiene "gota",
        // "cartagena" contiene "carta"— así que va de último recurso y con
        // palabras de cinco letras para arriba.
        if (permitirRescate && puntuados.Count == 0)
        {
            foreach (var (emoji, palabras) in Indice)
            {
                foreach (var termino in terminos)
                {
                    if (palabras.Any(p => p.Length >= 5 && termino.Contains(p, StringComparison.Ordinal)))
                    {
                        puntuados.Add((emoji, 1));
                        break;
                    }
                }
            }
        }

        return puntuados
            .OrderByDescending(p => p.Puntos)
            .Select(p => p.Emoji)
            .Distinct()
            .Take(cuantos)
            .ToList();
    }

    /// <summary>
    /// Sin tildes y en minúscula. Se reemplaza letra por letra en vez de
    /// usar String.Normalize, que en Blazor WebAssembly lanza
    /// PlatformNotSupportedException: el runtime del navegador no trae las
    /// tablas de normalización de Unicode.
    /// </summary>
    private static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return "";

        const string ConTilde = "áàäâãéèëêíìïîóòöôõúùüûñç";
        const string SinTilde = "aaaaaeeeeiiiiooooouuuunc";

        var construido = new System.Text.StringBuilder(texto.Length);

        foreach (var c in texto.ToLowerInvariant())
        {
            var i = ConTilde.IndexOf(c);
            construido.Append(i >= 0 ? SinTilde[i] : char.IsLetterOrDigit(c) ? c : ' ');
        }

        return construido.ToString();
    }
}
