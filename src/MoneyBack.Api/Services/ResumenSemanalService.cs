using Microsoft.EntityFrameworkCore;
using MoneyBack.Api.Data;
using MoneyBack.Api.Models.DiaADia;

namespace MoneyBack.Api.Services;

/// <summary>
/// Segundo BackgroundService del proyecto. A diferencia de
/// RevisionSuscripcionesService (que no depende del reloj, solo del estado
/// guardado), este sí apunta a una hora concreta — "domingo en la noche,
/// hora Colombia" — así que necesita convertir explícitamente: Fly.io corre
/// en UTC puro y no hereda ninguna zona horaria gratis (a diferencia del
/// frontend Blazor WASM, que sí la toma del navegador). Revisa cada hora en
/// vez de cada 6 para no perder la ventana angosta del domingo en la noche.
///
/// Idempotencia: UltimoResumenEnviado en Usuario, revisando "hace más de 6
/// días". Se acepta conscientemente un riesgo de duplicado teórico si dos
/// máquinas de Fly.io corrieran a la vez exactamente en la misma hora — a
/// diferencia de ConfirmacionCobro, un push semanal duplicado ocasional es
/// cosmético, no amerita la complejidad de un índice único.
/// </summary>
public class ResumenSemanalService(IServiceScopeFactory scopeFactory, ILogger<ResumenSemanalService> logger)
    : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromHours(1);
    private static readonly TimeZoneInfo ZonaBogota = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Intervalo);
        do
        {
            try
            {
                var horaBogota = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ZonaBogota);
                if (horaBogota.DayOfWeek == DayOfWeek.Sunday && horaBogota.Hour is >= 19 and < 21)
                {
                    using var scope = scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var sender = scope.ServiceProvider.GetRequiredService<PushNotificationSender>();
                    await EnviarResumenesAsync(db, sender, horaBogota, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error enviando resúmenes semanales.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private static async Task EnviarResumenesAsync(ApplicationDbContext db, PushNotificationSender sender, DateTime hoyBogota, CancellationToken ct)
    {
        var haceUnaSemana = hoyBogota.AddDays(-6);

        var usuarios = await db.Users
            .Where(u => u.UltimoResumenEnviado == null || u.UltimoResumenEnviado < haceUnaSemana)
            .ToListAsync(ct);

        foreach (var usuario in usuarios)
        {
            var inicioSemana = hoyBogota.Date.AddDays(-6);

            var movimientos = await db.MovimientosDiaADia
                .Include(m => m.Categoria)
                .Where(m => m.UsuarioId == usuario.Id && m.Categoria.Tipo == TipoCategoria.Gasto && m.Fecha >= inicioSemana)
                .ToListAsync(ct);

            if (movimientos.Count == 0) continue;

            var totalSemana = movimientos.Sum(m => m.Monto);
            var categoriaTop = movimientos
                .GroupBy(m => m.Categoria.Nombre)
                .OrderByDescending(g => g.Sum(m => m.Monto))
                .First();

            var tip = await InsightsService.CalcularTipAsync(usuario.Id, db, hoyBogota);

            var cuerpo = $"Esta semana gastaste ${totalSemana:N0}, la mayoría en {categoriaTop.Key} (${categoriaTop.Sum(m => m.Monto):N0}).";
            if (tip is not null) cuerpo += $" {tip}";

            usuario.UltimoResumenEnviado = hoyBogota;
            await db.SaveChangesAsync(ct);

            await sender.EnviarATodosLosDispositivosAsync(usuario.Id, "Tu resumen de la semana", cuerpo);
        }
    }
}
