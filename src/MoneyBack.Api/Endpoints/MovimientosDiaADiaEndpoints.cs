using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MoneyBack.Api.Data;
using MoneyBack.Api.Dtos;
using MoneyBack.Api.Models.DiaADia;
using MoneyBack.Api.Services;

namespace MoneyBack.Api.Endpoints;

public static class MovimientosDiaADiaEndpoints
{
    public static void MapMovimientosDiaADiaEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/movimientos-diaadia").WithTags("DiaADia").RequireAuthorization();

        group.MapGet("/", async (DateTime? desde, DateTime? hasta, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var usuarioId = principal.GetUsuarioId();
            var query = db.MovimientosDiaADia.Include(m => m.Categoria).Where(m => m.UsuarioId == usuarioId);

            if (desde is not null) query = query.Where(m => m.Fecha >= AComoUtc(desde.Value));
            if (hasta is not null) query = query.Where(m => m.Fecha <= AComoUtc(hasta.Value));

            var movimientos = await query
                .OrderByDescending(m => m.Fecha)
                .Select(m => new MovimientoDiaADiaResponse(
                    m.Id, m.CategoriaId, m.Categoria.Nombre, m.Categoria.Icono, m.Categoria.Tipo,
                    m.Monto, m.Fecha, m.Nota, m.RedondeoAplicado))
                .ToListAsync();

            return Results.Ok(movimientos);
        });

        group.MapGet("/resumen", async (DateTime? desde, DateTime? hasta, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var usuarioId = principal.GetUsuarioId();
            var query = db.MovimientosDiaADia.Include(m => m.Categoria).Where(m => m.UsuarioId == usuarioId);

            if (desde is not null) query = query.Where(m => m.Fecha >= AComoUtc(desde.Value));
            if (hasta is not null) query = query.Where(m => m.Fecha <= AComoUtc(hasta.Value));

            var movimientos = await query.ToListAsync();

            var totalIngresos = movimientos.Where(m => m.Categoria.Tipo == TipoCategoria.Ingreso).Sum(m => m.Monto);
            var totalGastos = movimientos.Where(m => m.Categoria.Tipo == TipoCategoria.Gasto).Sum(m => m.Monto);

            var gastosPorCategoria = movimientos
                .Where(m => m.Categoria.Tipo == TipoCategoria.Gasto)
                .GroupBy(m => m.Categoria)
                .Select(g => new TotalPorCategoria(g.Key.Id, g.Key.Nombre, g.Key.Icono, g.Sum(m => m.Monto)))
                .OrderByDescending(t => t.Total)
                .ToList();

            return Results.Ok(new ResumenDiaADiaResponse(totalIngresos, totalGastos, totalIngresos - totalGastos, gastosPorCategoria));
        });

        group.MapPost("/", async (CrearMovimientoDiaADiaRequest request, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            if (request.Monto <= 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["monto"] = ["El monto debe ser mayor a cero."]
                });
            }

            var usuarioId = principal.GetUsuarioId();
            var categoria = await db.Categorias.FirstOrDefaultAsync(c => c.Id == request.CategoriaId && c.UsuarioId == usuarioId);
            if (categoria is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["categoriaId"] = ["La categoría no existe."]
                });
            }

            var movimiento = new MovimientoDiaADia
            {
                UsuarioId = usuarioId,
                CategoriaId = categoria.Id,
                Monto = request.Monto,
                Fecha = request.Fecha ?? DateTime.UtcNow,
                Nota = request.Nota
            };
            db.MovimientosDiaADia.Add(movimiento);

            if (categoria.Tipo == TipoCategoria.Gasto)
            {
                await RedondeoService.AplicarSiCorrespondeAsync(movimiento, db);
            }

            await db.SaveChangesAsync();

            return Results.Created($"/api/movimientos-diaadia/{movimiento.Id}", new MovimientoDiaADiaResponse(
                movimiento.Id, categoria.Id, categoria.Nombre, categoria.Icono, categoria.Tipo,
                movimiento.Monto, movimiento.Fecha, movimiento.Nota, movimiento.RedondeoAplicado));
        });

        group.MapDelete("/{id:int}", async (int id, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var usuarioId = principal.GetUsuarioId();
            var movimiento = await db.MovimientosDiaADia.FirstOrDefaultAsync(m => m.Id == id && m.UsuarioId == usuarioId);
            if (movimiento is null) return Results.NotFound();

            db.MovimientosDiaADia.Remove(movimiento);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    /// <summary>
    /// Los query params desde/hasta llegan sin offset (Kind=Unspecified) y
    /// Npgsql exige Kind=Utc explícito para comparar contra una columna
    /// timestamptz — sin esto, cualquier filtro de fecha tira 500.
    /// </summary>
    private static DateTime AComoUtc(DateTime valor) =>
        valor.Kind == DateTimeKind.Utc ? valor : DateTime.SpecifyKind(valor, DateTimeKind.Utc);
}
