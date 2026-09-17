using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MoneyBack.Api.Data;
using MoneyBack.Api.Dtos;
using MoneyBack.Api.Models.Metas;
using MoneyBack.Api.Services;

namespace MoneyBack.Api.Endpoints;

public static class MetasEndpoints
{
    public static void MapMetasEndpoints(this WebApplication app)
    {
        var hogarMetas = app.MapGroup("/api/hogares/{hogarId:int}/metas").WithTags("Metas").RequireAuthorization();
        var metas = app.MapGroup("/api/metas").WithTags("Metas").RequireAuthorization();

        hogarMetas.MapPost("/", async (int hogarId, CrearMetaRequest request, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            if (request.MontoObjetivo <= 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["montoObjetivo"] = ["El monto objetivo debe ser mayor a cero."]
                });
            }

            var hogar = await db.Hogares.FindAsync(hogarId);
            if (hogar is null) return Results.NotFound($"No existe el hogar {hogarId}.");
            if (!hogar.PerteneceAlHogar(principal)) return Results.Forbid();

            var meta = new MetaAhorro
            {
                HogarId = hogarId,
                Tipo = request.Tipo,
                Nombre = request.Nombre,
                MontoObjetivo = request.MontoObjetivo
            };
            db.MetasAhorro.Add(meta);
            await db.SaveChangesAsync();

            return Results.Created($"/api/metas/{meta.Id}", ToResponse(meta));
        });

        hogarMetas.MapGet("/", async (int hogarId, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var hogar = await db.Hogares.FindAsync(hogarId);
            if (hogar is null) return Results.NotFound();
            if (!hogar.PerteneceAlHogar(principal)) return Results.Forbid();

            var listado = await db.MetasAhorro
                .Where(m => m.HogarId == hogarId)
                .Include(m => m.Movimientos)
                .OrderBy(m => m.FechaCreacion)
                .ToListAsync();

            return Results.Ok(listado.Select(ToResponse));
        });

        metas.MapGet("/{id:int}", async (int id, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var meta = await db.MetasAhorro
                .Include(m => m.Hogar)
                .Include(m => m.Movimientos)
                    .ThenInclude(mv => mv.Usuario)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (meta is null) return Results.NotFound();
            if (!meta.Hogar.PerteneceAlHogar(principal)) return Results.Forbid();

            var aportesPorUsuario = meta.Movimientos
                .GroupBy(mv => mv.Usuario)
                .Select(g => new AportePorUsuario(
                    g.Key.Id,
                    g.Key.Nombre,
                    g.Where(mv => mv.Tipo == TipoMovimiento.Aporte).Sum(mv => mv.Monto) -
                    g.Where(mv => mv.Tipo == TipoMovimiento.Retiro).Sum(mv => mv.Monto)))
                .ToList();

            var movimientos = meta.Movimientos
                .OrderByDescending(mv => mv.Fecha)
                .Select(mv => new MovimientoResponse(
                    mv.Id, mv.UsuarioId, mv.Usuario.Nombre, mv.Tipo, mv.Monto, mv.Fecha, mv.Nota, mv.EsAutomatico))
                .ToList();

            return Results.Ok(new MetaDetalleResponse(
                meta.Id, meta.HogarId, meta.Tipo, meta.Nombre, meta.MontoObjetivo,
                meta.MontoActual, meta.PorcentajeCompletado, meta.FechaObjetivoEstimada,
                meta.Activa, meta.FechaCreacion, aportesPorUsuario, movimientos));
        });

        metas.MapPost("/{id:int}/movimientos", async (int id, CrearMovimientoRequest request, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            if (request.Monto <= 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["monto"] = ["El monto debe ser mayor a cero."]
                });
            }

            var meta = await db.MetasAhorro
                .Include(m => m.Hogar)
                .Include(m => m.Movimientos)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (meta is null) return Results.NotFound($"No existe la meta {id}.");
            if (!meta.Hogar.PerteneceAlHogar(principal)) return Results.Forbid();
            if (!meta.Activa) return Results.Conflict("La meta está archivada, no admite movimientos nuevos.");

            if (request.Tipo == TipoMovimiento.Retiro && request.Monto > meta.MontoActual)
            {
                return Results.Conflict("El retiro supera el monto actual acumulado en la meta.");
            }

            var movimiento = new MovimientoMeta
            {
                MetaAhorroId = id,
                UsuarioId = principal.GetUsuarioId(),
                Tipo = request.Tipo,
                Monto = request.Monto,
                Nota = request.Nota,
                EsAutomatico = request.EsAutomatico
            };
            db.MovimientosMeta.Add(movimiento);
            await db.SaveChangesAsync();

            return Results.Created($"/api/metas/{id}/movimientos/{movimiento.Id}", movimiento.Id);
        });

        metas.MapGet("/{id:int}/movimientos", async (int id, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var meta = await db.MetasAhorro.Include(m => m.Hogar).FirstOrDefaultAsync(m => m.Id == id);
            if (meta is null) return Results.NotFound();
            if (!meta.Hogar.PerteneceAlHogar(principal)) return Results.Forbid();

            var movimientos = await db.MovimientosMeta
                .Where(mv => mv.MetaAhorroId == id)
                .Include(mv => mv.Usuario)
                .OrderByDescending(mv => mv.Fecha)
                .Select(mv => new MovimientoResponse(
                    mv.Id, mv.UsuarioId, mv.Usuario.Nombre, mv.Tipo, mv.Monto, mv.Fecha, mv.Nota, mv.EsAutomatico))
                .ToListAsync();

            return Results.Ok(movimientos);
        });

        metas.MapPost("/{id:int}/archivar", async (int id, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var meta = await db.MetasAhorro.Include(m => m.Hogar).FirstOrDefaultAsync(m => m.Id == id);
            if (meta is null) return Results.NotFound();
            if (!meta.Hogar.PerteneceAlHogar(principal)) return Results.Forbid();

            meta.Activa = false;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    private static MetaResponse ToResponse(MetaAhorro meta) => new(
        meta.Id, meta.HogarId, meta.Tipo, meta.Nombre, meta.MontoObjetivo,
        meta.MontoActual, meta.PorcentajeCompletado, meta.FechaObjetivoEstimada,
        meta.Activa, meta.FechaCreacion);
}
