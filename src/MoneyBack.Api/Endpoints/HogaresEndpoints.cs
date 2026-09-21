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

        // Ya no hay un POST "/" que cree el hogar directo — eso vinculaba a
        // dos personas solo con que una escribiera el correo de la otra,
        // sin que la invitada confirmara nada. Ver InvitacionesHogarEndpoints:
        // ahora un hogar solo nace cuando el invitado acepta la invitación.

        group.MapGet("/mio", async (ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var usuarioId = principal.GetUsuarioId();
            var hogar = await db.Hogares
                .Include(h => h.Usuario1)
                .Include(h => h.Usuario2)
                .FirstOrDefaultAsync(h => h.Usuario1Id == usuarioId || h.Usuario2Id == usuarioId);
            return hogar is null ? Results.NotFound() : Results.Ok(ToResponse(hogar, hogar.Usuario1.Nombre, hogar.Usuario2.Nombre));
        });

        group.MapPut("/mio", async (ActualizarHogarRequest request, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var usuarioId = principal.GetUsuarioId();
            var hogar = await db.Hogares
                .Include(h => h.Usuario1)
                .Include(h => h.Usuario2)
                .FirstOrDefaultAsync(h => h.Usuario1Id == usuarioId || h.Usuario2Id == usuarioId);
            if (hogar is null) return Results.NotFound();

            if (request.PorcentajeRedondeoEmergencia + request.PorcentajeRedondeoApartamento != 100)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["porcentajes"] = ["Los porcentajes de redondeo deben sumar 100."]
                });
            }

            hogar.AplicaTope150 = request.AplicaTope150;
            hogar.RedondeoActivo = request.RedondeoActivo;
            hogar.PorcentajeRedondeoEmergencia = request.PorcentajeRedondeoEmergencia;
            hogar.PorcentajeRedondeoApartamento = request.PorcentajeRedondeoApartamento;
            await db.SaveChangesAsync();

            return Results.Ok(ToResponse(hogar, hogar.Usuario1.Nombre, hogar.Usuario2.Nombre));
        });

        group.MapGet("/{id:int}", async (int id, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var hogar = await db.Hogares
                .Include(h => h.Usuario1)
                .Include(h => h.Usuario2)
                .FirstOrDefaultAsync(h => h.Id == id);
            if (hogar is null) return Results.NotFound();
            if (!hogar.PerteneceAlHogar(principal)) return Results.Forbid();

            return Results.Ok(ToResponse(hogar, hogar.Usuario1.Nombre, hogar.Usuario2.Nombre));
        });
    }

    private static HogarResponse ToResponse(Hogar hogar, string usuario1Nombre, string usuario2Nombre) => new(
        hogar.Id,
        hogar.Usuario1Id,
        usuario1Nombre,
        hogar.Usuario2Id,
        usuario2Nombre,
        hogar.AplicaTope150,
        hogar.RedondeoActivo,
        hogar.PorcentajeRedondeoEmergencia,
        hogar.PorcentajeRedondeoApartamento);
}
