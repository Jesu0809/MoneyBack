namespace MoneyBack.Web.Services;

/// <summary>
/// El catálogo de emojis del selector, por categorías.
///
/// Escribir el emoji a mano no es una forma razonable de elegir uno: hay que
/// saber abrir el teclado de emojis, buscarlo y que quede el correcto. Acá
/// se toca y ya.
///
/// Es una lista curada y no los ~1.900 de Unicode a propósito: pintar dos
/// mil botones hace lento el teléfono, y la mitad son variantes de tono de
/// piel y banderas que nadie usa para nombrar una meta de ahorro. Esto cubre
/// lo que la gente de verdad escoge; si falta alguno, se agrega acá.
/// </summary>
public static class CatalogoEmojis
{
    public record Categoria(string Nombre, string[] Emojis);

    public static readonly Categoria[] Categorias =
    [
        new("Dinero y casa", [
            "💰", "💵", "💸", "🪙", "💳", "🏦", "🧾", "📈", "📉", "🤑", "💎", "🏆",
            "🏠", "🏡", "🏘️", "🏢", "🏗️", "🔑", "🛋️", "🛏️", "🚪", "🪑", "🖼️", "🧱"
        ]),
        new("Cosas", [
            "🧺", "🧹", "🧼", "🪠", "🚿", "🛁", "🚽", "🪣", "🧊", "🍽️", "🍴", "🥄",
            "📱", "💻", "🖥️", "⌨️", "🖨️", "📷", "🎥", "📺", "🎧", "🔌", "🔋", "💡",
            "🪫", "🧯", "🧰", "🔧", "🔨", "🪚", "🪛", "⚙️", "🧲", "🪜", "🧸", "🎁"
        ]),
        new("Transporte", [
            "🚗", "🚙", "🏎️", "🛻", "🚐", "🚚", "🏍️", "🛵", "🚲", "🛴", "🛹", "🚕",
            "🚌", "🚎", "🚓", "🚑", "🚒", "✈️", "🛩️", "🚁", "🚀", "🛳️", "⛵", "🚤",
            "🚂", "🚆", "🚊", "🛣️", "⛽", "🧳", "🗺️", "🧭"
        ]),
        new("Comida", [
            "🍔", "🍕", "🌭", "🥪", "🌮", "🌯", "🥗", "🍜", "🍝", "🍲", "🍛", "🍚",
            "🍞", "🥐", "🥖", "🧀", "🥚", "🥓", "🍗", "🍖", "🥩", "🍤", "🍣", "🥘",
            "🍎", "🍌", "🍇", "🍓", "🍊", "🍋", "🍉", "🥑", "🥦", "🥕", "🌽", "🥔",
            "☕", "🍵", "🥤", "🧃", "🍺", "🍷", "🥂", "🍰", "🎂", "🍪", "🍫", "🍦"
        ]),
        new("Salud y estudio", [
            "🏥", "💊", "💉", "🩺", "🦷", "🧠", "🫀", "🩹", "🧴", "🪥", "👓", "🦽",
            "🎓", "📚", "📖", "📝", "✏️", "📐", "🔬", "🔭", "🧪", "🎒", "🏫", "🗂️"
        ]),
        new("Actividades", [
            "⚽", "🏀", "🏈", "⚾", "🎾", "🏐", "🏓", "🏸", "🥊", "🥋", "⛳", "🏹",
            "🎣", "🚴", "🏃", "🏊", "🧗", "⛷️", "🏂", "🏋️", "🧘", "🤸", "🎯", "🎮",
            "🎲", "🎸", "🎹", "🥁", "🎺", "🎨", "🎭", "🎬", "🎤", "🎧", "📸", "🎪"
        ]),
        new("Naturaleza y mascotas", [
            "🐶", "🐱", "🐭", "🐹", "🐰", "🦊", "🐻", "🐼", "🐨", "🐯", "🦁", "🐮",
            "🐷", "🐸", "🐵", "🐔", "🐧", "🐦", "🦆", "🦉", "🐴", "🦄", "🐝", "🐢",
            "🐠", "🐬", "🐳", "🌳", "🌴", "🌵", "🌻", "🌷", "🌹", "🍀", "🌈", "⭐",
            "🌙", "☀️", "⛅", "❄️", "🔥", "💧", "🌊", "🏔️", "🏖️", "🏝️", "🐾", "🪴"
        ]),
        new("Personas y fechas", [
            "👶", "🧒", "👦", "👧", "🧑", "👩", "👨", "🧓", "👵", "👴", "👪", "👨‍👩‍👧",
            "💑", "💍", "👰", "🤵", "🎉", "🎊", "🎈", "🥳", "🎄", "🎃", "🧑‍🎄", "🎆",
            "❤️", "💛", "💚", "💙", "💜", "🧡", "🤍", "💯"
        ]),
        new("Trabajo y símbolos", [
            "💼", "🧑‍💻", "👷", "🧑‍🍳", "🧑‍⚕️", "🧑‍🏫", "🧑‍🔧", "🧑‍🌾", "🏭", "🏪", "🛒", "📦",
            "📅", "⏰", "⌛", "🔔", "📌", "📎", "✅", "❗", "❓", "⚠️", "🛟", "♻️",
            "👕", "👗", "👖", "👟", "👞", "🧥", "🎒", "👜", "💄", "💅", "✂️", "🪒"
        ])
    ];
}
