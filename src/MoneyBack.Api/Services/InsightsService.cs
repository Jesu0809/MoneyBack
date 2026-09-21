using Microsoft.EntityFrameworkCore;
using MoneyBack.Api.Data;
using MoneyBack.Api.Models;
using MoneyBack.Api.Models.DiaADia;

namespace MoneyBack.Api.Services;

/// <summary>
/// Reglas simples sobre los propios datos del usuario — nada de IA real.
/// Calcula, en orden de prioridad, un único tip para mostrar en el resumen
/// semanal: (1) un presupuesto que va camino a superarse este mes, (2) el
/// ritmo de gasto general frente a los días que faltan para el próximo pago,
/// (3) si nada de lo anterior aplica, una comparación neutral contra el
/// promedio de los últimos 3 meses. Nunca se llama desde el flujo de
/// registrar un movimiento — el gasto ya ocurrió, ahí no hay nada que decidir.
/// </summary>
public static class InsightsService
{
    public static async Task<string?> CalcularTipAsync(int usuarioId, ApplicationDbContext db, DateTime hoyLocal)
    {
        return await TipSobregastoProyectadoAsync(usuarioId, db, hoyLocal)
            ?? await TipRitmoHastaProximoPagoAsync(usuarioId, db, hoyLocal)
            ?? await TipComparacionHistoricaAsync(usuarioId, db, hoyLocal);
    }

    private static async Task<string?> TipSobregastoProyectadoAsync(int usuarioId, ApplicationDbContext db, DateTime hoy)
    {
        var diasDelMes = DateTime.DaysInMonth(hoy.Year, hoy.Month);
        var diasTranscurridos = hoy.Day;

        var presupuestos = await db.Presupuestos
            .Include(p => p.Categoria)
            .Where(p => p.UsuarioId == usuarioId && p.Mes == hoy.Month && p.Anio == hoy.Year)
            .ToListAsync();

        foreach (var presupuesto in presupuestos)
        {
            var gastado = await db.MovimientosDiaADia
                .Where(m => m.UsuarioId == usuarioId && m.CategoriaId == presupuesto.CategoriaId
                    && m.Fecha.Year == hoy.Year && m.Fecha.Month == hoy.Month)
                .SumAsync(m => m.Monto);

            if (gastado <= 0) continue;

            var proyectado = gastado / diasTranscurridos * diasDelMes;
            if (proyectado > presupuesto.MontoLimite)
            {
                return $"Vas camino a superar tu presupuesto de {presupuesto.Categoria.Nombre} este mes " +
                       $"(proyectado ${proyectado:N0} de ${presupuesto.MontoLimite:N0}).";
            }
        }

        return null;
    }

    private static async Task<string?> TipRitmoHastaProximoPagoAsync(int usuarioId, ApplicationDbContext db, DateTime hoy)
    {
        var usuario = await db.Users.FirstOrDefaultAsync(u => u.Id == usuarioId);
        var diasPago = new[] { usuario?.DiaPago1, usuario?.DiaPago2 }
            .Where(d => d is > 0)
            .Select(d => d!.Value)
            .ToList();

        if (diasPago.Count == 0) return null;

        var diasHastaProximoPago = DiasHastaProximoPago(hoy, diasPago);

        var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);
        var gastoDelMes = await db.MovimientosDiaADia
            .Include(m => m.Categoria)
            .Where(m => m.UsuarioId == usuarioId && m.Categoria.Tipo == TipoCategoria.Gasto && m.Fecha >= inicioMes)
            .SumAsync(m => m.Monto);

        if (gastoDelMes <= 0) return null;

        var ritmoDiario = gastoDelMes / hoy.Day;

        // Días de holgura reales: cuánto exigiría el ritmo actual de aquí al
        // próximo pago, sin asumir nada del monto del ingreso — solo avisa
        // si falta bastante y el ritmo ya es alto en términos absolutos del mes.
        if (diasHastaProximoPago <= 5 && ritmoDiario * diasHastaProximoPago > gastoDelMes * 0.3m)
        {
            return $"Te faltan {diasHastaProximoPago} días para tu próximo pago y tu ritmo de gasto sigue alto — vale la pena frenar un poco.";
        }

        return null;
    }

    private static int DiasHastaProximoPago(DateTime hoy, List<int> diasPago)
    {
        var candidatos = new List<DateTime>();

        foreach (var dia in diasPago)
        {
            var diaAjustado = Math.Min(dia, DateTime.DaysInMonth(hoy.Year, hoy.Month));
            var fecha = new DateTime(hoy.Year, hoy.Month, diaAjustado);
            if (fecha < hoy.Date) fecha = fecha.AddMonths(1);
            candidatos.Add(fecha);
        }

        return candidatos.Min(f => (f - hoy.Date).Days);
    }

    private static async Task<string?> TipComparacionHistoricaAsync(int usuarioId, ApplicationDbContext db, DateTime hoy)
    {
        var inicioMesActual = new DateTime(hoy.Year, hoy.Month, 1);
        var inicioComparacion = inicioMesActual.AddMonths(-3);

        var gastoMesesAnteriores = await db.MovimientosDiaADia
            .Include(m => m.Categoria)
            .Where(m => m.UsuarioId == usuarioId && m.Categoria.Tipo == TipoCategoria.Gasto
                && m.Fecha >= inicioComparacion && m.Fecha < inicioMesActual)
            .SumAsync(m => m.Monto);

        if (gastoMesesAnteriores <= 0) return null;

        var promedioMensual = gastoMesesAnteriores / 3m;

        var gastoMesActual = await db.MovimientosDiaADia
            .Include(m => m.Categoria)
            .Where(m => m.UsuarioId == usuarioId && m.Categoria.Tipo == TipoCategoria.Gasto && m.Fecha >= inicioMesActual)
            .SumAsync(m => m.Monto);

        if (gastoMesActual <= 0) return null;

        var proyectado = gastoMesActual / hoy.Day * DateTime.DaysInMonth(hoy.Year, hoy.Month);

        return proyectado <= promedioMensual
            ? $"Vas bien: tu ritmo de este mes (${proyectado:N0} proyectado) está por debajo de tu promedio de los últimos 3 meses (${promedioMensual:N0})."
            : $"Este mes vas un poco por encima de tu promedio habitual (${promedioMensual:N0} en los últimos 3 meses).";
    }
}
