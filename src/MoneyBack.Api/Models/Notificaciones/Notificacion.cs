namespace MoneyBack.Api.Models.Notificaciones;

/// <summary>
/// De qué es el aviso. Sirve para el ícono y para poder filtrar después;
/// el texto ya viene escrito desde quien la creó.
/// </summary>
public enum TipoNotificacion
{
    /// <summary>Cualquier cosa que no encaje en las demás.</summary>
    General,

    /// <summary>Un tope de gasto al 80% o pasado.</summary>
    Tope,

    /// <summary>Un cobro fijo que se viene o que hay que confirmar.</summary>
    CobroFijo,

    /// <summary>El resumen de la semana.</summary>
    Resumen,

    /// <summary>Un gasto que entró solo desde el banco.</summary>
    GastoAutomatico,

    /// <summary>Alguien te invitó a un grupo.</summary>
    Invitacion
}

/// <summary>
/// El registro de un aviso que se le mandó a alguien.
///
/// Existe porque una notificación push que no se ve se pierde para siempre:
/// si el teléfono estaba en silencio, si se descartó sin leer, si el permiso
/// estaba negado, el aviso de que un tope se pasó simplemente nunca ocurrió
/// para esa persona. Con esto, el push es el aviso y esto es el registro —
/// y la campanita por fin tiene qué mostrar.
/// </summary>
public class Notificacion
{
    public int Id { get; set; }

    public int UsuarioId { get; set; }

    public TipoNotificacion Tipo { get; set; } = TipoNotificacion.General;

    public string Titulo { get; set; } = "";

    public string Cuerpo { get; set; } = "";

    /// <summary>A dónde lleva al tocarla. Nulo si no lleva a ningún lado.</summary>
    public string? Url { get; set; }

    public DateTime CreadaEn { get; set; } = DateTime.UtcNow;

    /// <summary>Nulo mientras no se haya leído.</summary>
    public DateTime? LeidaEn { get; set; }
}
