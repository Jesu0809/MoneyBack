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
            if (string.IsNullOrWhiteSpace(request.Nombre))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["nombre"] = ["Ponle un nombre a la meta."]
                });
            }

            if (request.MontoObjetivo <= 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["montoObjetivo"] = ["El monto objetivo debe ser mayor a cero."]
                });
            }

            var hogar = await db.Hogares.Include(h => h.Miembros).FirstOrDefaultAsync(h => h.Id == hogarId);
            if (hogar is null) return Results.NotFound($"No existe el hogar {hogarId}.");
            if (!hogar.PerteneceAlHogar(principal)) return Results.Forbid();

            var meta = new MetaAhorro
            {
                HogarId = hogarId,
                Icono = Emoji.Primero(request.Icono) ?? "🎯",
                EsFondoEmergencia = request.EsFondoEmergencia,
                // Se acota igual que al editar: un 99999% no rompe el
                // reparto —se normaliza— pero deja un dato que no significa
                // nada y que sorprende al leerlo.
                PorcentajeRedondeo = Math.Clamp(request.PorcentajeRedondeo, 0, 100),
                Nombre = request.Nombre.Trim(),
                MontoObjetivo = request.MontoObjetivo
            };
            db.MetasAhorro.Add(meta);
            await db.SaveChangesAsync();

            return Results.Created($"/api/metas/{meta.Id}", ToResponse(meta));
        });

        hogarMetas.MapGet("/", async (int hogarId, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var hogar = await db.Hogares.Include(h => h.Miembros).FirstOrDefaultAsync(h => h.Id == hogarId);
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
                .Include(m => m.Hogar).ThenInclude(h => h.Miembros)
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
                meta.Id, meta.HogarId, meta.Nombre, meta.Icono, meta.EsFondoEmergencia, meta.PorcentajeRedondeo, meta.MontoObjetivo,
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
                .Include(m => m.Hogar).ThenInclude(h => h.Miembros)
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
            var meta = await db.MetasAhorro.Include(m => m.Hogar).ThenInclude(h => h.Miembros).FirstOrDefaultAsync(m => m.Id == id);
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
            var meta = await db.MetasAhorro.Include(m => m.Hogar).ThenInclude(h => h.Miembros).FirstOrDefaultAsync(m => m.Id == id);
            if (meta is null) return Results.NotFound();
            if (!meta.Hogar.PerteneceAlHogar(principal)) return Results.Forbid();

            meta.Activa = false;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // Solo existía archivar. Como la pantalla lista únicamente las metas
        // activas, archivar una por error la hacía desaparecer sin forma de
        // recuperarla — con su historial de aportes adentro. Archivar tiene
        // que ser reversible, o es borrar con otro nombre.
        metas.MapPost("/{id:int}/desarchivar", async (int id, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var meta = await db.MetasAhorro.Include(m => m.Hogar).ThenInclude(h => h.Miembros).FirstOrDefaultAsync(m => m.Id == id);
            if (meta is null) return Results.NotFound();
            if (!meta.Hogar.PerteneceAlHogar(principal)) return Results.Forbid();

            meta.Activa = true;
            await db.SaveChangesAsync();
            return Results.Ok(ToResponse(meta));
        });

        // Editar: con metas de nombre e ícono libres, equivocarse al
        // escribirlas es cuestión de tiempo, y la única salida era archivar y
        // crear otra — perdiendo el historial de aportes.
        metas.MapPut("/{id:int}", async (
            int id, ActualizarMetaRequest request, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var meta = await db.MetasAhorro.Include(m => m.Hogar).ThenInclude(h => h.Miembros).FirstOrDefaultAsync(m => m.Id == id);
            if (meta is null) return Results.NotFound();
            if (!meta.Hogar.PerteneceAlHogar(principal)) return Results.Forbid();

            if (string.IsNullOrWhiteSpace(request.Nombre))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["nombre"] = ["Ponle un nombre a la meta."]
                });
            }

            if (request.MontoObjetivo <= 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["montoObjetivo"] = ["La meta debe ser mayor a cero."]
                });
            }

            meta.Nombre = request.Nombre.Trim();
            meta.MontoObjetivo = request.MontoObjetivo;
            meta.Icono = Emoji.Primero(request.Icono) ?? meta.Icono;
            meta.PorcentajeRedondeo = Math.Clamp(request.PorcentajeRedondeo, 0, 100);

            await db.SaveChangesAsync();
            return Results.Ok(ToResponse(meta));
        });

        // La meta favorita: a dónde apunta el botón de aportar del día a
        // día. Es de cada persona, no del grupo — dos personas del mismo
        // hogar pueden estar empujando cosas distintas.
        metas.MapPost("/{id:int}/favorita", async (
            int id, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var meta = await db.MetasAhorro
                .Include(m => m.Hogar).ThenInclude(h => h.Miembros)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (meta is null) return Results.NotFound();
            if (!meta.Hogar.PerteneceAlHogar(principal)) return Results.Forbid();

            var usuario = await db.Users.FindAsync(principal.GetUsuarioId());
            if (usuario is null) return Results.NotFound();

            usuario.MetaFavoritaId = meta.Id;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        metas.MapDelete("/favorita", async (ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var usuario = await db.Users.FindAsync(principal.GetUsuarioId());
            if (usuario is null) return Results.NotFound();

            usuario.MetaFavoritaId = null;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // A dónde debe llevar el botón de aportar, resuelto en el servidor
        // para que la app no tenga que traerse todos los grupos y todas las
        // metas solo para pintar un botón.
        metas.MapGet("/destino-aporte", async (ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var usuarioId = principal.GetUsuarioId();

            var activas = await db.MetasAhorro
                .Where(m => m.Activa && m.Hogar.Miembros.Any(mi => mi.UsuarioId == usuarioId))
                .Include(m => m.Hogar)
                .OrderBy(m => m.FechaCreacion)
                .ToListAsync();

            if (activas.Count == 0) return Results.Ok(new DestinoAporteResponse("SinMetas", null, null, null, 0));

            var usuario = await db.Users.FindAsync(usuarioId);
            var favorita = usuario?.MetaFavoritaId is int favId
                ? activas.FirstOrDefault(m => m.Id == favId)
                : null;

            // Si la favorita se archivó o se salió de ese grupo, se cae al
            // caso general en vez de mandar a una pantalla que ya no existe.
            var elegida = favorita ?? (activas.Count == 1 ? activas[0] : null);

            return Results.Ok(elegida is null
                ? new DestinoAporteResponse("Varias", null, null, null, activas.Count)
                : new DestinoAporteResponse(
                    favorita is not null ? "Favorita" : "Unica",
                    elegida.Id, elegida.Nombre, elegida.Icono, activas.Count));
        });
    }

    private static MetaResponse ToResponse(MetaAhorro meta) => new(
        meta.Id, meta.HogarId, meta.Nombre, meta.Icono, meta.EsFondoEmergencia, meta.PorcentajeRedondeo, meta.MontoObjetivo,
        meta.MontoActual, meta.PorcentajeCompletado, meta.FechaObjetivoEstimada,
        meta.Activa, meta.FechaCreacion);
}
