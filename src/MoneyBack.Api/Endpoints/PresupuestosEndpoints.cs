using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MoneyBack.Api.Data;
using MoneyBack.Api.Dtos;
using MoneyBack.Api.Models.DiaADia;
using MoneyBack.Api.Services;

namespace MoneyBack.Api.Endpoints;

public static class PresupuestosEndpoints
{
    public static void MapPresupuestosEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/presupuestos").WithTags("Presupuestos").RequireAuthorization();

        group.MapGet("/", async (int mes, int anio, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var usuarioId = principal.GetUsuarioId();

            var presupuestos = await db.Presupuestos
                .Include(p => p.Categoria)
                .Where(p => p.UsuarioId == usuarioId && p.Mes == mes && p.Anio == anio)
                .ToListAsync();

            var inicioMes = new DateTime(anio, mes, 1, 0, 0, 0, DateTimeKind.Utc);
            var finMes = inicioMes.AddMonths(1);

            var gastosDelMes = await db.MovimientosDiaADia
                .Where(m => m.UsuarioId == usuarioId && m.Fecha >= inicioMes && m.Fecha < finMes)
                .GroupBy(m => m.CategoriaId)
                .Select(g => new { CategoriaId = g.Key, Total = g.Sum(m => m.Monto) })
                .ToDictionaryAsync(g => g.CategoriaId, g => g.Total);

            var respuesta = presupuestos.Select(p => new PresupuestoResponse(
                p.Id, p.CategoriaId, p.Categoria.Nombre, p.Categoria.Icono, p.MontoLimite,
                gastosDelMes.GetValueOrDefault(p.CategoriaId), p.Mes, p.Anio));

            return Results.Ok(respuesta);
        });

        group.MapPost("/", async (GuardarPresupuestoRequest request, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            if (request.MontoLimite <= 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["montoLimite"] = ["El límite debe ser mayor a cero."]
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

            var existente = await db.Presupuestos.FirstOrDefaultAsync(p =>
                p.UsuarioId == usuarioId && p.CategoriaId == request.CategoriaId &&
                p.Mes == request.Mes && p.Anio == request.Anio);

            if (existente is not null)
            {
                existente.MontoLimite = request.MontoLimite;
            }
            else
            {
                db.Presupuestos.Add(new Presupuesto
                {
                    UsuarioId = usuarioId,
                    CategoriaId = request.CategoriaId,
                    MontoLimite = request.MontoLimite,
                    Mes = request.Mes,
                    Anio = request.Anio
                });
            }

            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        group.MapDelete("/{id:int}", async (int id, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var usuarioId = principal.GetUsuarioId();
            var presupuesto = await db.Presupuestos.FirstOrDefaultAsync(p => p.Id == id && p.UsuarioId == usuarioId);
            if (presupuesto is null) return Results.NotFound();

            db.Presupuestos.Remove(presupuesto);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}
