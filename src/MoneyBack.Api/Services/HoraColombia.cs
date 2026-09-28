namespace MoneyBack.Api.Services;

/// <summary>
/// La única forma correcta de mezclar "el calendario de acá" con "instantes
/// en la base".
///
/// El servidor corre en UTC y Postgres guarda `timestamp with time zone`,
/// que solo acepta fechas con Kind=Utc. Pero las decisiones del producto son
/// de calendario colombiano: "el primero del mes", "domingo en la noche",
/// "los últimos siete días". Y `TimeZoneInfo.ConvertTimeFromUtc` devuelve
/// Kind=Unspecified, así que el resultado sirve para razonar sobre días y
/// horas pero **revienta** apenas se usa en una consulta.
///
/// Eso ya pasó de verdad: el resumen semanal fallaba en la primera consulta
/// cada domingo y nunca envió ni uno solo. Las pruebas no lo veían porque
/// EF InMemory no valida el Kind — Postgres sí.
///
/// La regla: <see cref="Hoy"/> para pensar en fechas, y todo lo que vaya a
/// una consulta o a una columna pasa antes por <see cref="AInstanteUtc"/>.
/// </summary>
public static class HoraColombia
{
    public static readonly TimeZoneInfo Zona = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");

    /// <summary>
    /// La fecha y hora en Colombia ahora mismo. Kind=Unspecified a
    /// propósito: es un punto del calendario, no un instante, y no debe
    /// terminar en la base sin convertirse.
    /// </summary>
    public static DateTime Hoy() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zona);

    /// <summary>
    /// Convierte una hora del calendario colombiano al instante UTC que le
    /// corresponde, listo para comparar contra una columna o guardarse.
    /// </summary>
    public static DateTime AInstanteUtc(DateTime horaLocal) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(horaLocal, DateTimeKind.Unspecified), Zona);

    /// <summary>
    /// El instante UTC en que empezó el día colombiano de esa fecha.
    /// </summary>
    public static DateTime InicioDelDiaUtc(DateTime fechaLocal) => AInstanteUtc(fechaLocal.Date);

    /// <summary>
    /// El instante UTC en que empezó el mes colombiano de esa fecha.
    /// </summary>
    public static DateTime InicioDelMesUtc(DateTime fechaLocal) =>
        AInstanteUtc(new DateTime(fechaLocal.Year, fechaLocal.Month, 1, 0, 0, 0));
}
