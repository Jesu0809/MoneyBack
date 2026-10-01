using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MoneyBack.Api.Data;
using MoneyBack.Api.Dtos;
using MoneyBack.Api.Models;
using MoneyBack.Api.Models.Metas;
using MoneyBack.Api.Services;

using MoneyBack.Api.Models.Notificaciones;

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

            var pareja = await userManager.FindByEmailAsync(request.EmailPareja.Trim());
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

            // Se invita a un grupo concreto. Si no dicen a cuál, se usa el
            // primero de quien invita: es lo que hacía antes y evita romper
            // a quien solo tiene uno.
            var hogar = request.HogarId is int hogarId
                ? await db.Hogares.Include(h => h.Miembros)
                    .FirstOrDefaultAsync(h => h.Id == hogarId)
                : await db.Hogares.Include(h => h.Miembros)
                    .Where(h => h.Miembros.Any(m => m.UsuarioId == usuarioId))
                    .OrderBy(h => h.FechaCreacion)
                    .FirstOrDefaultAsync();

            if (hogar is null) return Results.NotFound("Ese grupo no existe. Crea uno antes de invitar.");
            if (!hogar.Miembros.Any(m => m.UsuarioId == usuarioId)) return Results.Forbid();

            if (hogar.Miembros.Any(m => m.UsuarioId == pareja.Id))
            {
                return Results.Conflict("Esa persona ya está en el grupo.");
            }

            var yaInvitada = await db.InvitacionesHogar.AnyAsync(i =>
                i.Estado == EstadoInvitacionHogar.Pendiente
                && i.HogarId == hogar.Id && i.InvitadoId == pareja.Id);
            if (yaInvitada)
            {
                return Results.Conflict("Esa persona ya tiene una invitación pendiente a este grupo.");
            }

            var invitacion = new InvitacionHogar
            {
                InvitadorId = usuarioId,
                InvitadoId = pareja.Id,
                HogarId = hogar.Id
            };
            db.InvitacionesHogar.Add(invitacion);
            await db.SaveChangesAsync();

            var yo = await userManager.FindByIdAsync(usuarioId.ToString());
            await sender.EnviarATodosLosDispositivosAsync(
                pareja.Id,
                "Invitación a un hogar",
                $"{yo!.Nombre} te invitó a crear un hogar en MoneyBack para ahorrar juntos.",
            tipo: TipoNotificacion.Invitacion);

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

            var hogar = await db.Hogares
                .Include(h => h.Miembros).ThenInclude(m => m.Usuario)
                .FirstOrDefaultAsync(h => h.Id == invitacion.HogarId);

            if (hogar is null) return Results.NotFound("Ese grupo ya no existe.");

            if (hogar.Miembros.Any(m => m.UsuarioId == invitacion.InvitadoId))
            {
                return Results.Conflict("Ya estás en ese grupo.");
            }

            // Aceptar suma a la persona al grupo. Antes CREABA un hogar, que
            // es lo que hacía imposible tener más de dos personas.
            db.MiembrosHogar.Add(new MiembroHogar
            {
                HogarId = hogar.Id,
                UsuarioId = invitacion.InvitadoId
            });

            invitacion.Estado = EstadoInvitacionHogar.Aceptada;
            invitacion.FechaResolucion = DateTime.UtcNow;

            await db.SaveChangesAsync();

            await db.Entry(hogar).Collection(h => h.Miembros).Query().Include(m => m.Usuario).LoadAsync();

            return Results.Ok(new HogarResponse(
                hogar.Id, hogar.Nombre, hogar.AplicaTope150, hogar.RedondeoActivo,
                SoyAdministrador: false,
                hogar.Miembros
                    .OrderByDescending(m => m.EsAdministrador).ThenBy(m => m.FechaIngreso)
                    .Select(m => new MiembroResponse(m.UsuarioId, m.Usuario.Nombre, m.EsAdministrador))
                    .ToList()));
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
