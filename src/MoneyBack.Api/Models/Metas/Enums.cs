namespace MoneyBack.Api.Models.Metas;

// Acá vivía TipoMeta, un enum con Apartamento y Emergencia. Se quitó porque
// la gente ahorra para una lavadora, un viaje o la matrícula, y un enum
// obligaba a que todo eso fuera "Apartamento". Ahora la meta lleva nombre
// libre e ícono, y el fondo de emergencia es una marca, no un tipo aparte.

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
