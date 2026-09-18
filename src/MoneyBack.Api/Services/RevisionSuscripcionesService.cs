using Microsoft.EntityFrameworkCore;
using MoneyBack.Api.Data;
using MoneyBack.Api.Models.Suscripciones;

namespace MoneyBack.Api.Services;

/// <summary>
/// Primer BackgroundService del proyecto. Cada Intervalo revisa las
/// suscripciones activas y crea una ConfirmacionCobro pendiente cuando el
/// próximo cobro cae dentro de la ventana de aviso — nunca crea el gasto
/// directamente, eso solo pasa cuando el usuario resuelve la confirmación.
///
/// No apunta a una hora exacta del día a propósito: la corrección depende
/// del estado guardado (¿ya existe una ConfirmacionCobro para este período?),
/// no del reloj, así que corre cada Intervalo sin más — el do/while cubre
/// el caso de que la máquina se haya reiniciado (revisa apenas arranca, no
/// espera el primer tick). Es seguro con fly.toml en min_machines_running=1;
/// si algún día hay más de una instancia corriendo a la vez, el índice único
/// (SuscripcionId, PeriodoCobro) en ConfirmacionCobroConfiguration evita
/// duplicados aunque el chequeo no sea atómico.
/// </summary>
public class RevisionSuscripcionesService(IServiceScopeFactory scopeFactory, ILogger<RevisionSuscripcionesService> logger)
    : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Intervalo);
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var sender = scope.ServiceProvider.GetRequiredService<PushNotificationSender>();
                await RevisarAsync(db, sender, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error revisando suscripciones pendientes de cobro.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>
    /// Guarda una por una (no todas en un solo SaveChangesAsync): con dos
    /// máquinas de Fly.io corriendo este servicio a la vez, es posible que
    /// ambas intenten crear la misma ConfirmacionCobro en el mismo ciclo —
    /// el índice único de SuscripcionId+PeriodoCobro hace que una de las dos
    /// falle. Si todo fuera un solo SaveChangesAsync, ese conflicto tumbaría
    /// también las confirmaciones nuevas y legítimas del mismo lote.
    /// </summary>
    private static async Task RevisarAsync(ApplicationDbContext db, PushNotificationSender sender, CancellationToken ct)
    {
        var hoy = DateTime.UtcNow.Date;

        var activas = await db.Suscripciones.Where(s => s.Activa).ToListAsync(ct);

        foreach (var suscripcion in activas)
        {
            if (suscripcion.ProximoCobro.Date > hoy.AddDays(suscripcion.DiasAvisoPrevio)) continue;

            var yaExiste = await db.ConfirmacionesCobro.AnyAsync(
                c => c.SuscripcionId == suscripcion.Id && c.PeriodoCobro == suscripcion.ProximoCobro, ct);
            if (yaExiste) continue;

            db.ConfirmacionesCobro.Add(new ConfirmacionCobro
            {
                SuscripcionId = suscripcion.Id,
                PeriodoCobro = suscripcion.ProximoCobro,
                Estado = EstadoConfirmacion.Pendiente
            });

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // La otra máquina ya la creó entre el chequeo y este guardado.
                // Se descarta el intento y se sigue con la siguiente suscripción.
                db.ChangeTracker.Clear();
                continue;
            }

            // Solo se envía si el guardado de arriba realmente creó la fila
            // (no en el caso del conflicto de la otra máquina) — evita un
            // push duplicado por el mismo cobro.
            await sender.EnviarATodosLosDispositivosAsync(
                suscripcion.UsuarioId,
                "Cobro próximo",
                $"{suscripcion.Nombre} se cobra el {suscripcion.ProximoCobro:d MMM} — confirma en MoneyBack si pasó.");
        }
    }
}
