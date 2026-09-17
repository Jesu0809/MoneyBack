using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MoneyBack.Api.Data;
using MoneyBack.Api.Dtos;
using MoneyBack.Api.Models.Metas;
using MoneyBack.Api.Services;

namespace MoneyBack.Api.Endpoints;

public static class HogaresEndpoints
{
    public static void MapHogaresEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/hogares").WithTags("Hogares").RequireAuthorization();

        group.MapPost("/", async (CrearHogarRequest request, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var usuarioId = principal.GetUsuarioId();

            if (usuarioId == request.UsuarioParejaId)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["usuarioParejaId"] = ["Un hogar necesita dos usuarios distintos."]
                });
            }

            if (request.PorcentajeRedondeoEmergencia + request.PorcentajeRedondeoApartamento != 100)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["porcentajes"] = ["Los porcentajes de redondeo deben sumar 100."]
                });
            }

            var parejaExiste = await db.Users.AnyAsync(u => u.Id == request.UsuarioParejaId);
            if (!parejaExiste)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["usuarioParejaId"] = ["El usuario indicado no existe."]
                });
            }

            var yaTieneHogar = await db.Hogares.AnyAsync(h =>
                h.Usuario1Id == usuarioId || h.Usuario2Id == usuarioId ||
                h.Usuario1Id == request.UsuarioParejaId || h.Usuario2Id == request.UsuarioParejaId);
            if (yaTieneHogar)
            {
                return Results.Conflict("Uno de los dos usuarios ya pertenece a un hogar.");
            }

            var hogar = new Hogar
            {
                Usuario1Id = usuarioId,
                Usuario2Id = request.UsuarioParejaId,
                AplicaTope150 = request.AplicaTope150,
                PorcentajeRedondeoEmergencia = request.PorcentajeRedondeoEmergencia,
                PorcentajeRedondeoApartamento = request.PorcentajeRedondeoApartamento
            };
            db.Hogares.Add(hogar);
            await db.SaveChangesAsync();

            return Results.Created($"/api/hogares/{hogar.Id}", ToResponse(hogar));
        });

        group.MapGet("/mio", async (ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var usuarioId = principal.GetUsuarioId();
            var hogar = await db.Hogares.FirstOrDefaultAsync(h => h.Usuario1Id == usuarioId || h.Usuario2Id == usuarioId);
            return hogar is null ? Results.NotFound() : Results.Ok(ToResponse(hogar));
        });

        group.MapPut("/mio", async (ActualizarHogarRequest request, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var usuarioId = principal.GetUsuarioId();
            var hogar = await db.Hogares.FirstOrDefaultAsync(h => h.Usuario1Id == usuarioId || h.Usuario2Id == usuarioId);
            if (hogar is null) return Results.NotFound();

            if (request.PorcentajeRedondeoEmergencia + request.PorcentajeRedondeoApartamento != 100)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["porcentajes"] = ["Los porcentajes de redondeo deben sumar 100."]
                });
            }

            hogar.AplicaTope150 = request.AplicaTope150;
            hogar.PorcentajeRedondeoEmergencia = request.PorcentajeRedondeoEmergencia;
            hogar.PorcentajeRedondeoApartamento = request.PorcentajeRedondeoApartamento;
            await db.SaveChangesAsync();

            return Results.Ok(ToResponse(hogar));
        });

        group.MapGet("/{id:int}", async (int id, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var hogar = await db.Hogares.FindAsync(id);
            if (hogar is null) return Results.NotFound();
            if (!hogar.PerteneceAlHogar(principal)) return Results.Forbid();

            return Results.Ok(ToResponse(hogar));
        });
    }

    private static HogarResponse ToResponse(Hogar hogar) => new(
        hogar.Id,
        hogar.Usuario1Id,
        hogar.Usuario2Id,
        hogar.AplicaTope150,
        hogar.PorcentajeRedondeoEmergencia,
        hogar.PorcentajeRedondeoApartamento);
}
