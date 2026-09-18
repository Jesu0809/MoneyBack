using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MoneyBack.Api.Data;
using MoneyBack.Api.Dtos;
using MoneyBack.Api.Models.DiaADia;
using MoneyBack.Api.Models.Suscripciones;
using MoneyBack.Api.Services;

namespace MoneyBack.Api.Endpoints;

public static class SuscripcionesEndpoints
{
    public static void MapSuscripcionesEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/suscripciones").WithTags("Suscripciones").RequireAuthorization();

        group.MapGet("/", async (ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var usuarioId = principal.GetUsuarioId();
            var suscripciones = await db.Suscripciones
                .Include(s => s.Categoria)
                .Where(s => s.UsuarioId == usuarioId)
                .OrderBy(s => s.ProximoCobro)
                .ToListAsync();

            return Results.Ok(suscripciones.Select(ToResponse));
        });

        group.MapGet("/confirmaciones-pendientes", async (ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var usuarioId = principal.GetUsuarioId();
            var pendientes = await db.ConfirmacionesCobro
                .Include(c => c.Suscripcion).ThenInclude(s => s.Categoria)
                .Where(c => c.Estado == EstadoConfirmacion.Pendiente && c.Suscripcion.UsuarioId == usuarioId)
                .OrderBy(c => c.PeriodoCobro)
                .Select(c => new ConfirmacionPendienteResponse(
                    c.Id, c.SuscripcionId, c.Suscripcion.Nombre, c.Suscripcion.Monto,
                    c.Suscripcion.Categoria.Icono, c.PeriodoCobro))
                .ToListAsync();

            return Results.Ok(pendientes);
        });

        group.MapPost("/", async (CrearSuscripcionRequest request, ClaimsPrincipal principal, ApplicationDbContext db) =>
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

            var suscripcion = new Suscripcion
            {
                UsuarioId = usuarioId,
                Nombre = request.Nombre,
                Monto = request.Monto,
                CategoriaId = categoria.Id,
                Frecuencia = request.Frecuencia,
                ProximoCobro = AComoUtc(request.ProximoCobro),
                DiasAvisoPrevio = request.DiasAvisoPrevio
            };
            db.Suscripciones.Add(suscripcion);
            await db.SaveChangesAsync();

            suscripcion.Categoria = categoria;
            return Results.Created($"/api/suscripciones/{suscripcion.Id}", ToResponse(suscripcion));
        });

        group.MapPut("/{id:int}", async (int id, ActualizarSuscripcionRequest request, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            if (request.Monto <= 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["monto"] = ["El monto debe ser mayor a cero."]
                });
            }

            var usuarioId = principal.GetUsuarioId();
            var suscripcion = await db.Suscripciones.Include(s => s.Categoria)
                .FirstOrDefaultAsync(s => s.Id == id && s.UsuarioId == usuarioId);
            if (suscripcion is null) return Results.NotFound();

            var categoria = await db.Categorias.FirstOrDefaultAsync(c => c.Id == request.CategoriaId && c.UsuarioId == usuarioId);
            if (categoria is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["categoriaId"] = ["La categoría no existe."]
                });
            }

            suscripcion.Nombre = request.Nombre;
            suscripcion.Monto = request.Monto;
            suscripcion.CategoriaId = categoria.Id;
            suscripcion.Frecuencia = request.Frecuencia;
            suscripcion.DiasAvisoPrevio = request.DiasAvisoPrevio;
            suscripcion.Activa = request.Activa;
            await db.SaveChangesAsync();

            suscripcion.Categoria = categoria;
            return Results.Ok(ToResponse(suscripcion));
        });

        group.MapDelete("/{id:int}", async (int id, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var usuarioId = principal.GetUsuarioId();
            var suscripcion = await db.Suscripciones.FirstOrDefaultAsync(s => s.Id == id && s.UsuarioId == usuarioId);
            if (suscripcion is null) return Results.NotFound();

            db.Suscripciones.Remove(suscripcion);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        group.MapPost("/{id:int}/confirmaciones/{confirmacionId:int}/resolver",
            async (int id, int confirmacionId, ResolverConfirmacionRequest request, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var usuarioId = principal.GetUsuarioId();
            var confirmacion = await db.ConfirmacionesCobro
                .Include(c => c.Suscripcion).ThenInclude(s => s.Categoria)
                .FirstOrDefaultAsync(c => c.Id == confirmacionId && c.SuscripcionId == id);

            if (confirmacion is null) return Results.NotFound();
            if (confirmacion.Suscripcion.UsuarioId != usuarioId) return Results.Forbid();
            if (confirmacion.Estado != EstadoConfirmacion.Pendiente)
            {
                return Results.Conflict("Esta confirmación ya fue resuelta.");
            }

            if (request.Ocurrio)
            {
                var suscripcion = confirmacion.Suscripcion;
                var gasto = new MovimientoDiaADia
                {
                    UsuarioId = usuarioId,
                    CategoriaId = suscripcion.CategoriaId,
                    Monto = suscripcion.Monto,
                    Fecha = confirmacion.PeriodoCobro,
                    Nota = $"Suscripción: {suscripcion.Nombre}"
                };
                db.MovimientosDiaADia.Add(gasto);

                if (suscripcion.Categoria.Tipo == TipoCategoria.Gasto)
                {
                    await RedondeoService.AplicarSiCorrespondeAsync(gasto, db);
                }

                await db.SaveChangesAsync();

                confirmacion.MovimientoDiaADiaId = gasto.Id;
                confirmacion.Estado = EstadoConfirmacion.Confirmado;
            }
            else
            {
                confirmacion.Estado = EstadoConfirmacion.Rechazado;
            }

            confirmacion.FechaResolucion = DateTime.UtcNow;
            AvanzarProximoCobro(confirmacion.Suscripcion);

            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    private static void AvanzarProximoCobro(Suscripcion suscripcion)
    {
        suscripcion.ProximoCobro = suscripcion.Frecuencia switch
        {
            FrecuenciaSuscripcion.Semanal => suscripcion.ProximoCobro.AddDays(7),
            FrecuenciaSuscripcion.Mensual => suscripcion.ProximoCobro.AddMonths(1),
            FrecuenciaSuscripcion.Anual => suscripcion.ProximoCobro.AddYears(1),
            _ => throw new InvalidOperationException($"Frecuencia desconocida: {suscripcion.Frecuencia}")
        };
    }

    /// <summary>
    /// El date picker del frontend manda un DateTime con Kind=Unspecified y
    /// Npgsql exige Kind=Utc explícito para timestamptz — mismo problema ya
    /// resuelto en MovimientosDiaADiaEndpoints.
    /// </summary>
    private static DateTime AComoUtc(DateTime valor) =>
        valor.Kind == DateTimeKind.Utc ? valor : DateTime.SpecifyKind(valor, DateTimeKind.Utc);

    private static SuscripcionResponse ToResponse(Suscripcion s) => new(
        s.Id, s.Nombre, s.Monto, s.CategoriaId, s.Categoria.Nombre, s.Categoria.Icono,
        s.Frecuencia, s.ProximoCobro, s.DiasAvisoPrevio, s.Activa);
}
