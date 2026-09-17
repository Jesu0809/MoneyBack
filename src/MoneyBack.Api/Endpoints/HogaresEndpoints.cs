using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MoneyBack.Api.Data;
using MoneyBack.Api.Dtos;
using MoneyBack.Api.Models;
using MoneyBack.Api.Models.Metas;
using MoneyBack.Api.Services;

namespace MoneyBack.Api.Endpoints;

public static class HogaresEndpoints
{
    public static void MapHogaresEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/hogares").WithTags("Hogares").RequireAuthorization();

        group.MapPost("/", async (
            CrearHogarRequest request,
            ClaimsPrincipal principal,
            ApplicationDbContext db,
            UserManager<Usuario> userManager) =>
        {
            var usuarioId = principal.GetUsuarioId();

            if (request.PorcentajeRedondeoEmergencia + request.PorcentajeRedondeoApartamento != 100)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["porcentajes"] = ["Los porcentajes de redondeo deben sumar 100."]
                });
            }

            var pareja = await userManager.FindByEmailAsync(request.EmailPareja);
            if (pareja is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["emailPareja"] = ["No encontramos una cuenta con ese correo. Tu pareja debe registrarse primero."]
                });
            }

            if (pareja.Id == usuarioId)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["emailPareja"] = ["Un hogar necesita dos usuarios distintos."]
                });
            }

            var yaTieneHogar = await db.Hogares.AnyAsync(h =>
                h.Usuario1Id == usuarioId || h.Usuario2Id == usuarioId ||
                h.Usuario1Id == pareja.Id || h.Usuario2Id == pareja.Id);
            if (yaTieneHogar)
            {
                return Results.Conflict("Uno de los dos usuarios ya pertenece a un hogar.");
            }

            var hogar = new Hogar
            {
                Usuario1Id = usuarioId,
                Usuario2Id = pareja.Id,
                AplicaTope150 = request.AplicaTope150,
                PorcentajeRedondeoEmergencia = request.PorcentajeRedondeoEmergencia,
                PorcentajeRedondeoApartamento = request.PorcentajeRedondeoApartamento
            };
            db.Hogares.Add(hogar);
            await db.SaveChangesAsync();

            var yo = await userManager.FindByIdAsync(usuarioId.ToString());
            return Results.Created($"/api/hogares/{hogar.Id}", ToResponse(hogar, yo!.Nombre, pareja.Nombre));
        });

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
