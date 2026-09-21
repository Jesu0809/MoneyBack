namespace MoneyBack.Api.Models.Metas;

/// <summary>
/// Tipo de meta de ahorro compartida entre los dos usuarios del hogar.
/// Un solo enum en vez de tablas separadas, para poder agregar nuevos
/// tipos de meta en el futuro sin duplicar estructura.
/// </summary>
public enum TipoMeta
{
    Apartamento,
    Emergencia
}

/// <summary>
/// Un movimiento dentro de una MetaAhorro puede ser un aporte (suma) o
/// un retiro (resta). El monto siempre se guarda en positivo; el signo
/// lo determina el Tipo.
/// </summary>
public enum TipoMovimiento
{
    Aporte,
    Retiro
}

/// <summary>
/// Vincular un hogar ya no es inmediato: el invitado debe aceptar. Mientras
/// tanto la invitación existe sola, sin crear ningún Hogar todavía.
/// </summary>
public enum EstadoInvitacionHogar
{
    Pendiente,
    Aceptada,
    Rechazada
}
