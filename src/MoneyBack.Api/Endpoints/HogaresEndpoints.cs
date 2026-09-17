using Microsoft.EntityFrameworkCore;
using MoneyBack.Api.Data;
using MoneyBack.Api.Dtos;
using MoneyBack.Api.Models.Metas;

namespace MoneyBack.Api.Endpoints;

public static class HogaresEndpoints
{
    public static void MapHogaresEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/hogares").WithTags("Hogares");

        group.MapPost("/", async (CrearHogarRequest request, ApplicationDbContext db) =>
        {
            if (request.Usuario1Id == request.Usuario2Id)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["usuarios"] = ["Un hogar necesita dos usuarios distintos."]
                });
            }

            if (request.PorcentajeRedondeoEmergencia + request.PorcentajeRedondeoApartamento != 100)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["porcentajes"] = ["Los porcentajes de redondeo deben sumar 100."]
                });
            }

            var usuariosExistentes = await db.Usuarios
                .CountAsync(u => u.Id == request.Usuario1Id || u.Id == request.Usuario2Id);
            if (usuariosExistentes != 2)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["usuarios"] = ["Usuario1Id y Usuario2Id deben existir."]
                });
            }

            var hogar = new Hogar
            {
                Usuario1Id = request.Usuario1Id,
                Usuario2Id = request.Usuario2Id,
                PorcentajeRedondeoEmergencia = request.PorcentajeRedondeoEmergencia,
                PorcentajeRedondeoApartamento = request.PorcentajeRedondeoApartamento
            };
            db.Hogares.Add(hogar);
            await db.SaveChangesAsync();

            var response = ToResponse(hogar);
            return Results.Created($"/api/hogares/{hogar.Id}", response);
        });

        group.MapGet("/{id:int}", async (int id, ApplicationDbContext db) =>
        {
            var hogar = await db.Hogares.FindAsync(id);
            return hogar is null ? Results.NotFound() : Results.Ok(ToResponse(hogar));
        });
    }

    private static HogarResponse ToResponse(Hogar hogar) => new(
        hogar.Id,
        hogar.Usuario1Id,
        hogar.Usuario2Id,
        hogar.PorcentajeRedondeoEmergencia,
        hogar.PorcentajeRedondeoApartamento);
}
