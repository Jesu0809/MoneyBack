namespace MoneyBack.Web.Services;

/// <summary>
/// El calendario del teléfono, no el del servidor.
///
/// Acá todo se guarda como instante UTC, que es lo correcto. El problema es
/// decidir a qué DÍA y a qué MES pertenece ese instante: eso no se decide en
/// UTC, se decide donde está la persona.
///
/// Bogotá va cinco horas detrás, así que desde las 7 de la noche en UTC ya
/// es el día siguiente. Un gasto del 30 de septiembre a las 8:15 p.m. se
/// guarda como 1 de octubre a la 1:15 UTC — correcto como instante, pero si
/// el mes se calcula con UtcNow, la app abre en octubre estando en
/// septiembre y ese gasto cuenta contra el mes equivocado. Pasó, y se vio
/// en pantalla.
///
/// La regla, igual que HoraColombia en el servidor: <see cref="Hoy"/> para
/// razonar sobre fechas, y <see cref="AInstanteUtc"/> para cualquier cosa
/// que se mande al API.
///
/// Se usa la zona del navegador y no Bogotá fija a propósito: si viajan, el
/// día que ven en el teléfono es el que cuenta.
/// </summary>
public static class HoraLocal
{
    /// <summary>
    /// La fecha y hora donde está la persona. Kind=Local: no sirve para
    /// comparar contra lo guardado sin convertir antes.
    /// </summary>
    public static DateTime Hoy() => DateTime.Now;

    /// <summary>El primero de ese mes, a medianoche, hora de acá.</summary>
    public static DateTime InicioDeMes(DateTime local) => new(local.Year, local.Month, 1, 0, 0, 0, DateTimeKind.Local);

    /// <summary>El primero de enero de ese año, a medianoche, hora de acá.</summary>
    public static DateTime InicioDeAnio(int anio) => new(anio, 1, 1, 0, 0, 0, DateTimeKind.Local);

    /// <summary>
    /// Convierte una fecha del calendario de acá al instante UTC que le
    /// corresponde, que es lo que entiende el API.
    /// </summary>
    public static DateTime AInstanteUtc(DateTime local) =>
        DateTime.SpecifyKind(local, DateTimeKind.Local).ToUniversalTime();

    /// <summary>
    /// El último instante de ese mes, en UTC. Se calcula como "el inicio del
    /// mes siguiente menos un tic" y no como "día 30 a las 23:59" para no
    /// tener que pensar en meses de 28, 30 o 31 días.
    /// </summary>
    public static DateTime FinDeMesUtc(DateTime mesLocal) =>
        AInstanteUtc(InicioDeMes(mesLocal).AddMonths(1)).AddTicks(-1);

    public static DateTime InicioDeMesUtc(DateTime mesLocal) => AInstanteUtc(InicioDeMes(mesLocal));
}
