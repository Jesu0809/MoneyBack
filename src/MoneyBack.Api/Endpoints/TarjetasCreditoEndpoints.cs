using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MoneyBack.Api.Data;
using MoneyBack.Api.Dtos;
using MoneyBack.Api.Models.Tarjetas;
using MoneyBack.Api.Services;

namespace MoneyBack.Api.Endpoints;

public static class TarjetasCreditoEndpoints
{
    public static void MapTarjetasCreditoEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tarjetas-credito").WithTags("TarjetasCredito").RequireAuthorization();

        group.MapGet("/", async (ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var usuarioId = principal.GetUsuarioId();
            var tarjetas = await db.TarjetasCredito
                .Where(t => t.UsuarioId == usuarioId)
                .OrderBy(t => t.Nombre)
                .Select(t => new TarjetaCreditoResponse(t.Id, t.Nombre, t.DiaCorte, t.Activa))
                .ToListAsync();

            return Results.Ok(tarjetas);
        });

        group.MapPost("/", async (CrearTarjetaCreditoRequest request, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var error = ValidarDiaCorte(request.DiaCorte);
            if (error is not null) return error;

            var tarjeta = new TarjetaCredito
            {
                UsuarioId = principal.GetUsuarioId(),
                Nombre = request.Nombre,
                DiaCorte = request.DiaCorte
            };
            db.TarjetasCredito.Add(tarjeta);
            await db.SaveChangesAsync();

            return Results.Created($"/api/tarjetas-credito/{tarjeta.Id}", new TarjetaCreditoResponse(tarjeta.Id, tarjeta.Nombre, tarjeta.DiaCorte, tarjeta.Activa));
        });

        group.MapPut("/{id:int}", async (int id, ActualizarTarjetaCreditoRequest request, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var error = ValidarDiaCorte(request.DiaCorte);
            if (error is not null) return error;

            var usuarioId = principal.GetUsuarioId();
            var tarjeta = await db.TarjetasCredito.FirstOrDefaultAsync(t => t.Id == id && t.UsuarioId == usuarioId);
            if (tarjeta is null) return Results.NotFound();

            tarjeta.Nombre = request.Nombre;
            tarjeta.DiaCorte = request.DiaCorte;
            tarjeta.Activa = request.Activa;
            await db.SaveChangesAsync();

            return Results.Ok(new TarjetaCreditoResponse(tarjeta.Id, tarjeta.Nombre, tarjeta.DiaCorte, tarjeta.Activa));
        });

        group.MapDelete("/{id:int}", async (int id, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var usuarioId = principal.GetUsuarioId();
            var tarjeta = await db.TarjetasCredito.FirstOrDefaultAsync(t => t.Id == id && t.UsuarioId == usuarioId);
            if (tarjeta is null) return Results.NotFound();

            // Seguro de borrar: TarjetaCreditoConfiguration fija SetNull en
            // la FK de MovimientoDiaADia, así que los gastos ya registrados
            // con esta tarjeta quedan intactos, solo pierden la etiqueta.
            db.TarjetasCredito.Remove(tarjeta);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        group.MapGet("/{id:int}/saldo-pendiente", async (int id, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var usuarioId = principal.GetUsuarioId();
            var tarjeta = await db.TarjetasCredito.FirstOrDefaultAsync(t => t.Id == id && t.UsuarioId == usuarioId);
            if (tarjeta is null) return Results.NotFound();

            var totalGastos = await db.MovimientosDiaADia
                .Where(m => m.TarjetaCreditoId == id)
                .SumAsync(m => (decimal?)m.Monto) ?? 0;
            var totalPagos = await db.PagosTarjeta
                .Where(p => p.TarjetaCreditoId == id)
                .SumAsync(p => (decimal?)p.Monto) ?? 0;
            var ultimoPago = await db.PagosTarjeta
                .Where(p => p.TarjetaCreditoId == id)
                .OrderByDescending(p => p.Fecha)
                .Select(p => (DateTime?)p.Fecha)
                .FirstOrDefaultAsync();

            return Results.Ok(new SaldoPendienteResponse(totalGastos - totalPagos, ultimoPago));
        });

        group.MapPost("/{id:int}/pagos", async (int id, RegistrarPagoTarjetaRequest request, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            if (request.Monto <= 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["monto"] = ["El monto debe ser mayor a cero."]
                });
            }

            var usuarioId = principal.GetUsuarioId();
            var tarjeta = await db.TarjetasCredito.FirstOrDefaultAsync(t => t.Id == id && t.UsuarioId == usuarioId);
            if (tarjeta is null) return Results.NotFound();

            // A propósito NO crea un MovimientoDiaADia: las compras ya
            // restaron el saldo acumulado cuando se registraron. Esto es
            // solo la constancia de que ya se le pagó al banco.
            db.PagosTarjeta.Add(new PagoTarjeta
            {
                TarjetaCreditoId = id,
                Monto = request.Monto
            });
            await db.SaveChangesAsync();

            return Results.NoContent();
        });
    }

    private static IResult? ValidarDiaCorte(int diaCorte) =>
        diaCorte is < 1 or > 31
            ? Results.ValidationProblem(new Dictionary<string, string[]> { ["diaCorte"] = ["Debe ser un día del 1 al 31."] })
            : null;
}
