using Microsoft.EntityFrameworkCore;
using MoneyBack.Api.Data;
using MoneyBack.Api.Models.DiaADia;

using MoneyBack.Api.Models.Notificaciones;

namespace MoneyBack.Api.Services;

/// <summary>
/// Avisa por push cuando una categoría se acerca a su tope o se pasa.
///
/// Es el contrapeso del atajo. Desde que los gastos entran solos, ya no hay
/// un momento en que la persona vea cuánto lleva gastado: registrar dejó de
/// ser un acto consciente. Sin algo que hable solo, el tope existe pero nadie
/// se entera de que lo está rompiendo hasta fin de mes, cuando ya no se puede
/// hacer nada.
///
/// Se llama después de guardar el movimiento, nunca antes: un aviso sobre un
/// gasto que no alcanzó a registrarse sería mentira.
/// </summary>
public static class AlertasPresupuestoService
{
    /// <summary>
    /// 80 avisa cuando todavía queda mes por delante y se puede frenar; 100
    /// avisa lo ya consumado, que igual hay que saber. Más umbrales serían
    /// más ruido sin más decisiones posibles.
    /// </summary>
    private static readonly int[] Umbrales = [100, 80];

    public static async Task RevisarAsync(
        int usuarioId, int categoriaId, ApplicationDbContext db, PushNotificationSender sender)
    {
        var hoy = FechaBogota();

        var tope = await ObtenerTopeVigenteAsync(usuarioId, categoriaId, hoy.Month, hoy.Year, db);
        if (tope is null || tope.MontoLimite <= 0) return;

        // El mes de Colombia expresado en instantes UTC, no el mes de UTC.
        // Entre las 7pm y la medianoche de Bogotá el servidor ya está en el
        // día siguiente, así que el último día del mes las compras de la noche
        // contarían contra el mes entrante y el aviso hablaría de un tope que
        // la persona siente que apenas empieza.
        var inicioMes = AInstanteUtc(new DateTime(hoy.Year, hoy.Month, 1, 0, 0, 0));
        var finMes = AInstanteUtc(new DateTime(hoy.Year, hoy.Month, 1, 0, 0, 0).AddMonths(1));

        var gastado = await db.MovimientosDiaADia
            .Where(m => m.UsuarioId == usuarioId && m.CategoriaId == categoriaId
                && m.Fecha >= inicioMes && m.Fecha < finMes)
            .SumAsync(m => (decimal?)m.Monto) ?? 0m;

        var porcentaje = (int)Math.Floor(gastado / tope.MontoLimite * 100);

        // De mayor a menor: si una sola compra saltó del 60% al 110%, importa
        // que se pasó, no que cruzó el 80% en el camino.
        var umbral = Umbrales.FirstOrDefault(u => porcentaje >= u);
        if (umbral == 0) return;

        var categoria = await db.Categorias.FirstOrDefaultAsync(c => c.Id == categoriaId);
        if (categoria is null) return;

        var yaAvisado = await db.AvisosPresupuesto.AnyAsync(a =>
            a.UsuarioId == usuarioId && a.CategoriaId == categoriaId
            && a.Mes == hoy.Month && a.Anio == hoy.Year && a.Umbral >= umbral);

        if (yaAvisado) return;

        db.AvisosPresupuesto.Add(new AvisoPresupuesto
        {
            UsuarioId = usuarioId,
            CategoriaId = categoriaId,
            Mes = hoy.Month,
            Anio = hoy.Year,
            Umbral = umbral
        });

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Otro gasto simultáneo ganó la carrera y ya avisó. El índice
            // único hizo su trabajo: acá solo hay que no avisar de nuevo.
            return;
        }

        await sender.EnviarATodosLosDispositivosAsync(
            usuarioId,
            Titulo(categoria, umbral),
            Cuerpo(categoria, gastado, tope.MontoLimite, porcentaje, umbral, hoy),
            "/presupuestos",
            tipo: TipoNotificacion.Tope);
    }

    /// <summary>
    /// El tope del mes si existe; si no, el más reciente que se haya puesto.
    ///
    /// Sin este arrastre, un tope definido en enero no existiría en febrero y
    /// habría que volver a crearlo cada mes — algo que nadie hace, y que
    /// dejaría los avisos callados justo cuando más sirven. Poner un tope es
    /// declarar una intención, no llenar una casilla de un mes.
    /// </summary>
    public static async Task<Presupuesto?> ObtenerTopeVigenteAsync(
        int usuarioId, int categoriaId, int mes, int anio, ApplicationDbContext db)
    {
        return await db.Presupuestos
            .Where(p => p.UsuarioId == usuarioId && p.CategoriaId == categoriaId
                && (p.Anio < anio || (p.Anio == anio && p.Mes <= mes)))
            .OrderByDescending(p => p.Anio).ThenByDescending(p => p.Mes)
            .FirstOrDefaultAsync();
    }

    private static string Titulo(Categoria categoria, int umbral) =>
        umbral >= 100
            ? $"{categoria.Icono} Te pasaste en {categoria.Nombre}"
            : $"{categoria.Icono} {categoria.Nombre} va en {umbral}%";

    private static string Cuerpo(
        Categoria categoria, decimal gastado, decimal limite, int porcentaje, int umbral, DateTime hoy)
    {
        var llevas = $"{Formato.Pesos(gastado)} de {Formato.Pesos(limite)}";

        if (umbral >= 100)
        {
            return $"Llevas {llevas}. Te pasaste por {Formato.Pesos(gastado - limite)}.";
        }

        var diasQueQuedan = DateTime.DaysInMonth(hoy.Year, hoy.Month) - hoy.Day;
        var quedan = diasQueQuedan switch
        {
            0 => "Es el último día del mes.",
            1 => "Queda 1 día del mes.",
            _ => $"Quedan {diasQueQuedan} días del mes."
        };

        return $"Llevas {llevas} ({porcentaje}%). {quedan}";
    }

    /// <summary>
    /// El servidor corre en UTC, donde ya es el día siguiente desde las 7pm de
    /// Colombia. Usar esa fecha haría que el aviso hable de "quedan N días"
    /// con un número distinto al del calendario de quien lo lee, y peor: el
    /// último día del mes contaría el gasto contra el mes entrante.
    /// </summary>
    private static readonly TimeZoneInfo Bogota = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");

    private static DateTime FechaBogota() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Bogota);

    private static DateTime AInstanteUtc(DateTime horaLocal) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(horaLocal, DateTimeKind.Unspecified), Bogota);
}
