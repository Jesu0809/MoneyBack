using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MoneyBack.Api.Data;
using MoneyBack.Api.Dtos;
using MoneyBack.Api.Models.DiaADia;
using MoneyBack.Api.Models.Deudas;
using MoneyBack.Api.Services;

namespace MoneyBack.Api.Endpoints;

public static class DeudasEndpoints
{
    public static void MapDeudasEndpoints(this WebApplication app)
    {
        var deudas = app.MapGroup("/api/deudas").WithTags("Deudas").RequireAuthorization();
        var hogarDeudas = app.MapGroup("/api/hogares/{hogarId:int}/deudas").WithTags("Deudas").RequireAuthorization();

        deudas.MapGet("/", async (ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var usuarioId = principal.GetUsuarioId();
            var hogar = await db.Hogares
                .FirstOrDefaultAsync(h => h.Usuario1Id == usuarioId || h.Usuario2Id == usuarioId);

            var query = db.Deudas.Where(d =>
                d.UsuarioId == usuarioId || (hogar != null && d.HogarId == hogar.Id));

            var listado = await query.OrderBy(d => d.FechaCreacion).ToListAsync();
            return Results.Ok(listado.Select(ToResponse));
        });

        deudas.MapPost("/privada", async (CrearDeudaPrivadaRequest request, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var error = ValidarMontos(request.MontoTotal, request.MontoCuota, request.TotalCuotas);
            if (error is not null) return error;

            var deuda = new Deuda
            {
                TipoPropiedad = TipoPropiedadDeuda.Privada,
                UsuarioId = principal.GetUsuarioId(),
                Nombre = request.Nombre,
                MontoTotal = request.MontoTotal,
                MontoCuota = request.MontoCuota,
                TotalCuotas = request.TotalCuotas
            };
            db.Deudas.Add(deuda);
            await db.SaveChangesAsync();

            return Results.Created($"/api/deudas/{deuda.Id}", ToResponse(deuda));
        });

        hogarDeudas.MapPost("/", async (int hogarId, CrearDeudaCompartidaRequest request, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var error = ValidarMontos(request.MontoTotal, request.MontoCuota, request.TotalCuotas);
            if (error is not null) return error;

            var hogar = await db.Hogares.FindAsync(hogarId);
            if (hogar is null) return Results.NotFound($"No existe el hogar {hogarId}.");
            if (!hogar.PerteneceAlHogar(principal)) return Results.Forbid();

            var deuda = new Deuda
            {
                TipoPropiedad = TipoPropiedadDeuda.Compartida,
                HogarId = hogarId,
                Nombre = request.Nombre,
                MontoTotal = request.MontoTotal,
                MontoCuota = request.MontoCuota,
                TotalCuotas = request.TotalCuotas
            };
            db.Deudas.Add(deuda);
            await db.SaveChangesAsync();

            return Results.Created($"/api/deudas/{deuda.Id}", ToResponse(deuda));
        });

        deudas.MapPost("/{id:int}/pagar-cuota", async (int id, PagarCuotaRequest request, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var usuarioId = principal.GetUsuarioId();
            var deuda = await db.Deudas.Include(d => d.Hogar).FirstOrDefaultAsync(d => d.Id == id);
            if (deuda is null) return Results.NotFound();
            if (!deuda.PerteneceALaDeuda(principal)) return Results.Forbid();
            if (!deuda.Activa) return Results.Conflict("La deuda está archivada, no admite pagos nuevos.");
            if (deuda.CuotasPagadas >= deuda.TotalCuotas) return Results.Conflict("Ya se pagaron todas las cuotas.");

            var categoria = await db.Categorias.FirstOrDefaultAsync(c => c.Id == request.CategoriaId && c.UsuarioId == usuarioId);
            if (categoria is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["categoriaId"] = ["La categoría no existe."]
                });
            }

            // Igual que cualquier gasto manual: sale del bolsillo de quien
            // paga, así que se ve en su día a día — sea la deuda privada o
            // compartida (confirmado con el usuario: "el dinero no sale de
            // la nada").
            var gasto = new MovimientoDiaADia
            {
                UsuarioId = usuarioId,
                CategoriaId = categoria.Id,
                Monto = deuda.MontoCuota,
                Nota = string.IsNullOrWhiteSpace(request.Nota) ? $"Cuota: {deuda.Nombre}" : request.Nota
            };
            db.MovimientosDiaADia.Add(gasto);

            if (categoria.Tipo == TipoCategoria.Gasto)
            {
                await RedondeoService.AplicarSiCorrespondeAsync(gasto, db);
            }

            await db.SaveChangesAsync();

            db.PagosDeuda.Add(new PagoDeuda
            {
                DeudaId = deuda.Id,
                UsuarioId = usuarioId,
                Monto = deuda.MontoCuota,
                MovimientoDiaADiaId = gasto.Id
            });
            deuda.CuotasPagadas += 1;

            await db.SaveChangesAsync();

            return Results.Ok(ToResponse(deuda));
        });

        deudas.MapDelete("/{id:int}", async (int id, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var deuda = await db.Deudas.Include(d => d.Hogar).FirstOrDefaultAsync(d => d.Id == id);
            if (deuda is null) return Results.NotFound();
            if (!deuda.PerteneceALaDeuda(principal)) return Results.Forbid();

            db.Deudas.Remove(deuda);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    private static IResult? ValidarMontos(decimal montoTotal, decimal montoCuota, int totalCuotas)
    {
        var errores = new Dictionary<string, string[]>();
        if (montoTotal <= 0) errores["montoTotal"] = ["El monto total debe ser mayor a cero."];
        if (montoCuota <= 0) errores["montoCuota"] = ["El monto de la cuota debe ser mayor a cero."];
        if (totalCuotas <= 0) errores["totalCuotas"] = ["El número de cuotas debe ser mayor a cero."];
        return errores.Count > 0 ? Results.ValidationProblem(errores) : null;
    }

    private static DeudaResponse ToResponse(Deuda d) => new(
        d.Id, d.TipoPropiedad, d.Nombre, d.MontoTotal, d.MontoCuota, d.TotalCuotas, d.CuotasPagadas,
        d.MontoRestante, d.PorcentajePagado, d.Activa);
}
