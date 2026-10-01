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

            // El tope vigente de cada categoría: el de este mes si se definió
            // uno, y si no, el último que se haya puesto. Un tope es una
            // intención ("no quiero gastar más de esto en comida"), no un dato
            // de un mes; obligar a redefinirlo cada 30 días haría que casi
            // nadie tuviera topes en marzo, y sin topes no hay avisos.
            var presupuestos = await db.Presupuestos
                .Include(p => p.Categoria)
                .Where(p => p.UsuarioId == usuarioId && (p.Anio < anio || (p.Anio == anio && p.Mes <= mes)))
                .GroupBy(p => p.CategoriaId)
                .Select(g => g.OrderByDescending(p => p.Anio).ThenByDescending(p => p.Mes).First())
                .ToListAsync();

            // La medianoche de Bogotá, no la de UTC. Con UTC, los gastos
            // hechos entre las 7 p.m. y la medianoche del último día del mes
            // caían en el mes siguiente — y peor: AlertasPresupuestoService
            // ya usaba el corte colombiano, así que el aviso push y esta
            // pantalla hablaban de meses distintos y no cuadraban.
            var primeroLocal = new DateTime(anio, mes, 1, 0, 0, 0);
            var inicioMes = HoraColombia.AInstanteUtc(primeroLocal);
            var finMes = HoraColombia.AInstanteUtc(primeroLocal.AddMonths(1));

            var gastosDelMes = await db.MovimientosDiaADia
                .Where(m => m.UsuarioId == usuarioId && m.Fecha >= inicioMes && m.Fecha < finMes)
                .GroupBy(m => m.CategoriaId)
                .Select(g => new { CategoriaId = g.Key, Total = g.Sum(m => m.Monto) })
                .ToDictionaryAsync(g => g.CategoriaId, g => g.Total);

            var respuesta = presupuestos.Select(p => new PresupuestoResponse(
                p.Id, p.CategoriaId, p.Categoria.Nombre, p.Categoria.Icono, p.MontoLimite,
                gastosDelMes.GetValueOrDefault(p.CategoriaId), mes, anio,
                Heredado: p.Mes != mes || p.Anio != anio));

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
