using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MoneyBack.Api.Data;
using MoneyBack.Api.Dtos;
using MoneyBack.Api.Models;
using MoneyBack.Api.Models.Metas;
using MoneyBack.Api.Services;

namespace MoneyBack.Api.Endpoints;

public static class InvitacionesHogarEndpoints
{
    public static void MapInvitacionesHogarEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/invitaciones-hogar").WithTags("InvitacionesHogar").RequireAuthorization();

        group.MapPost("/", async (
            CrearInvitacionHogarRequest request,
            ClaimsPrincipal principal,
            ApplicationDbContext db,
            UserManager<Usuario> userManager,
            PushNotificationSender sender) =>
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

            var yaTieneInvitacionPendiente = await db.InvitacionesHogar.AnyAsync(i =>
                i.Estado == EstadoInvitacionHogar.Pendiente &&
                (i.InvitadorId == usuarioId || i.InvitadoId == usuarioId ||
                 i.InvitadorId == pareja.Id || i.InvitadoId == pareja.Id));
            if (yaTieneInvitacionPendiente)
            {
                return Results.Conflict("Uno de los dos ya tiene una invitación de hogar pendiente.");
            }

            var invitacion = new InvitacionHogar
            {
                InvitadorId = usuarioId,
                InvitadoId = pareja.Id,
                AplicaTope150 = request.AplicaTope150,
                PorcentajeRedondeoEmergencia = request.PorcentajeRedondeoEmergencia,
                PorcentajeRedondeoApartamento = request.PorcentajeRedondeoApartamento
            };
            db.InvitacionesHogar.Add(invitacion);
            await db.SaveChangesAsync();

            var yo = await userManager.FindByIdAsync(usuarioId.ToString());
            await sender.EnviarATodosLosDispositivosAsync(
                pareja.Id,
                "Invitación a un hogar",
                $"{yo!.Nombre} te invitó a crear un hogar en MoneyBack para ahorrar juntos.");

            return Results.Created($"/api/invitaciones-hogar/{invitacion.Id}", ToResponse(invitacion, yo.Nombre, pareja.Nombre));
        });

        group.MapGet("/mia", async (ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var usuarioId = principal.GetUsuarioId();

            var recibida = await db.InvitacionesHogar
                .Include(i => i.Invitador).Include(i => i.Invitado)
                .Where(i => i.InvitadoId == usuarioId && i.Estado == EstadoInvitacionHogar.Pendiente)
                .OrderByDescending(i => i.FechaCreacion)
                .FirstOrDefaultAsync();

            var enviada = await db.InvitacionesHogar
                .Include(i => i.Invitador).Include(i => i.Invitado)
                .Where(i => i.InvitadorId == usuarioId && i.Estado == EstadoInvitacionHogar.Pendiente)
                .OrderByDescending(i => i.FechaCreacion)
                .FirstOrDefaultAsync();

            return Results.Ok(new MisInvitacionesHogarResponse(
                recibida is null ? null : ToResponse(recibida, recibida.Invitador.Nombre, recibida.Invitado.Nombre),
                enviada is null ? null : ToResponse(enviada, enviada.Invitador.Nombre, enviada.Invitado.Nombre)));
        });

        group.MapPost("/{id:int}/aceptar", async (
            int id,
            ClaimsPrincipal principal,
            ApplicationDbContext db,
            UserManager<Usuario> userManager) =>
        {
            var usuarioId = principal.GetUsuarioId();
            var invitacion = await db.InvitacionesHogar
                .Include(i => i.Invitador).Include(i => i.Invitado)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (invitacion is null) return Results.NotFound();
            if (invitacion.InvitadoId != usuarioId) return Results.Forbid();
            if (invitacion.Estado != EstadoInvitacionHogar.Pendiente)
            {
                return Results.Conflict("Esta invitación ya se resolvió.");
            }

            var yaTieneHogar = await db.Hogares.AnyAsync(h =>
                h.Usuario1Id == invitacion.InvitadorId || h.Usuario2Id == invitacion.InvitadorId ||
                h.Usuario1Id == invitacion.InvitadoId || h.Usuario2Id == invitacion.InvitadoId);
            if (yaTieneHogar)
            {
                return Results.Conflict("Uno de los dos ya pertenece a un hogar.");
            }

            var hogar = new Hogar
            {
                Usuario1Id = invitacion.InvitadorId,
                Usuario2Id = invitacion.InvitadoId,
                AplicaTope150 = invitacion.AplicaTope150,
                PorcentajeRedondeoEmergencia = invitacion.PorcentajeRedondeoEmergencia,
                PorcentajeRedondeoApartamento = invitacion.PorcentajeRedondeoApartamento
            };
            db.Hogares.Add(hogar);

            invitacion.Estado = EstadoInvitacionHogar.Aceptada;
            invitacion.FechaResolucion = DateTime.UtcNow;

            await db.SaveChangesAsync();

            return Results.Ok(new HogarResponse(
                hogar.Id, hogar.Usuario1Id, invitacion.Invitador.Nombre, hogar.Usuario2Id, invitacion.Invitado.Nombre,
                hogar.AplicaTope150, hogar.RedondeoActivo, hogar.PorcentajeRedondeoEmergencia, hogar.PorcentajeRedondeoApartamento));
        });

        group.MapPost("/{id:int}/rechazar", async (int id, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var usuarioId = principal.GetUsuarioId();
            var invitacion = await db.InvitacionesHogar.FirstOrDefaultAsync(i => i.Id == id);

            if (invitacion is null) return Results.NotFound();
            // Tanto el invitado (rechaza) como quien invitó (cancela) pueden resolverla así.
            if (invitacion.InvitadoId != usuarioId && invitacion.InvitadorId != usuarioId) return Results.Forbid();
            if (invitacion.Estado != EstadoInvitacionHogar.Pendiente)
            {
                return Results.Conflict("Esta invitación ya se resolvió.");
            }

            invitacion.Estado = EstadoInvitacionHogar.Rechazada;
            invitacion.FechaResolucion = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Results.NoContent();
        });
    }

    private static InvitacionHogarResponse ToResponse(InvitacionHogar invitacion, string invitadorNombre, string invitadoNombre) => new(
        invitacion.Id,
        invitacion.InvitadorId,
        invitadorNombre,
        invitacion.InvitadoId,
        invitadoNombre,
        invitacion.Estado,
        invitacion.FechaCreacion);
}
