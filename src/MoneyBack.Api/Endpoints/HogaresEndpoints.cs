using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MoneyBack.Api.Data;
using MoneyBack.Api.Dtos;
using MoneyBack.Api.Models.Metas;
using MoneyBack.Api.Services;

namespace MoneyBack.Api.Endpoints;

/// <summary>
/// Grupos de ahorro. Se siguen llamando "hogares" en las rutas y en la base
/// para no romper lo que ya existe, pero ya no son un hogar de dos: son
/// grupos con las personas que sean, y una misma persona puede estar en
/// varios —el de la pareja, el de la familia, el del viaje con amigos.
/// </summary>
public static class HogaresEndpoints
{
    public static void MapHogaresEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/hogares").WithTags("Hogares").RequireAuthorization();

        group.MapGet("/", async (ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var usuarioId = principal.GetUsuarioId();

            var grupos = await db.Hogares
                .Where(h => h.Miembros.Any(m => m.UsuarioId == usuarioId))
                .Include(h => h.Miembros).ThenInclude(m => m.Usuario)
                .OrderBy(h => h.FechaCreacion)
                .ToListAsync();

            return Results.Ok(grupos.Select(g => ToResponse(g, usuarioId)).ToList());
        });

        // Crear un grupo ya no necesita a nadie más: antes un hogar solo
        // nacía cuando alguien aceptaba una invitación, así que era imposible
        // empezar a ahorrar solo y sumar gente después. Ahora se crea vacío y
        // se invita cuando se quiera.
        group.MapPost("/", async (CrearHogarRequest request, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(request.Nombre))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["nombre"] = ["Ponle un nombre al grupo."]
                });
            }

            var usuarioId = principal.GetUsuarioId();

            var hogar = new Hogar
            {
                Nombre = request.Nombre.Trim(),
                AplicaTope150 = request.AplicaTope150
            };
            hogar.Miembros.Add(new MiembroHogar { UsuarioId = usuarioId, EsAdministrador = true });

            db.Hogares.Add(hogar);
            await db.SaveChangesAsync();

            await db.Entry(hogar).Collection(h => h.Miembros).Query().Include(m => m.Usuario).LoadAsync();
            return Results.Created($"/api/hogares/{hogar.Id}", ToResponse(hogar, usuarioId));
        });

        group.MapGet("/mio/redondeo-total", async (ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var usuarioId = principal.GetUsuarioId();

            var total = await db.MovimientosMeta
                .Where(m => m.EsAutomatico
                    && m.MetaAhorro.Hogar.Miembros.Any(mi => mi.UsuarioId == usuarioId))
                .SumAsync(m => (decimal?)m.Monto) ?? 0m;

            return Results.Ok(new RedondeoTotalResponse(total));
        });

        group.MapGet("/mio", async (ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var usuarioId = principal.GetUsuarioId();

            var hogar = await CargarDelUsuarioAsync(usuarioId, db);
            return hogar is null ? Results.NoContent() : Results.Ok(ToResponse(hogar, usuarioId));
        });

        group.MapPut("/mio", async (ActualizarHogarRequest request, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var usuarioId = principal.GetUsuarioId();

            var hogar = await CargarDelUsuarioAsync(usuarioId, db);
            if (hogar is null) return Results.NotFound("Todavía no tienes un grupo.");

            hogar.AplicaTope150 = request.AplicaTope150;
            hogar.RedondeoActivo = request.RedondeoActivo;
            await db.SaveChangesAsync();

            return Results.Ok(ToResponse(hogar, usuarioId));
        });

        group.MapGet("/{id:int}", async (int id, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var hogar = await CargarAsync(id, db);
            if (hogar is null) return Results.NotFound();
            if (!hogar.PerteneceAlHogar(principal)) return Results.Forbid();

            return Results.Ok(ToResponse(hogar, principal.GetUsuarioId()));
        });

        group.MapPut("/{id:int}", async (
            int id, ActualizarGrupoRequest request, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var hogar = await CargarAsync(id, db);
            if (hogar is null) return Results.NotFound();
            if (!hogar.PerteneceAlHogar(principal)) return Results.Forbid();

            // Renombrar lo hace solo quien lo creó: el nombre es cómo los
            // demás reconocen el grupo en su lista, y que cualquiera pueda
            // cambiárselo a todos es pedir confusión.
            if (!hogar.EsAdministrador(principal)) return Results.Forbid();

            if (string.IsNullOrWhiteSpace(request.Nombre))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["nombre"] = ["Ponle un nombre al grupo."]
                });
            }

            hogar.Nombre = request.Nombre.Trim();
            hogar.AplicaTope150 = request.AplicaTope150;
            hogar.RedondeoActivo = request.RedondeoActivo;
            await db.SaveChangesAsync();

            return Results.Ok(ToResponse(hogar, principal.GetUsuarioId()));
        });

        group.MapPost("/{id:int}/salir", async (int id, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var hogar = await CargarAsync(id, db);
            if (hogar is null) return Results.NotFound();
            if (!hogar.PerteneceAlHogar(principal)) return Results.Forbid();

            var usuarioId = principal.GetUsuarioId();
            var yo = hogar.Miembros.First(m => m.UsuarioId == usuarioId);

            // Un grupo sin nadie adentro deja la plata huérfana: nadie puede
            // verla ni sacarla. Antes de irse hay que pasarle el grupo a
            // alguien, o no queda a quién.
            if (hogar.Miembros.Count == 1)
            {
                return Results.Conflict("Eres la única persona del grupo. Invita a alguien antes de salirte, o el ahorro quedaría sin dueño.");
            }

            // Si se va quien administraba, el más antiguo de los que quedan
            // toma el relevo — un grupo sin administrador no se puede
            // renombrar ni invitar a nadie más.
            if (yo.EsAdministrador && !hogar.Miembros.Any(m => m.UsuarioId != usuarioId && m.EsAdministrador))
            {
                var relevo = hogar.Miembros
                    .Where(m => m.UsuarioId != usuarioId)
                    .OrderBy(m => m.FechaIngreso)
                    .First();
                relevo.EsAdministrador = true;
            }

            db.MiembrosHogar.Remove(yo);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    private static Task<Hogar?> CargarAsync(int id, ApplicationDbContext db) =>
        db.Hogares
            .Include(h => h.Miembros).ThenInclude(m => m.Usuario)
            .FirstOrDefaultAsync(h => h.Id == id);

    /// <summary>
    /// El primer grupo de la persona. Existe para las pantallas que todavía
    /// asumen uno solo; las nuevas piden la lista y dejan elegir.
    /// </summary>
    private static Task<Hogar?> CargarDelUsuarioAsync(int usuarioId, ApplicationDbContext db) =>
        db.Hogares
            .Where(h => h.Miembros.Any(m => m.UsuarioId == usuarioId))
            .Include(h => h.Miembros).ThenInclude(m => m.Usuario)
            .OrderBy(h => h.FechaCreacion)
            .FirstOrDefaultAsync();

    private static HogarResponse ToResponse(Hogar hogar, int usuarioId) => new(
        hogar.Id,
        hogar.Nombre,
        hogar.AplicaTope150,
        hogar.RedondeoActivo,
        hogar.Miembros.Any(m => m.UsuarioId == usuarioId && m.EsAdministrador),
        hogar.Miembros
            .OrderByDescending(m => m.EsAdministrador).ThenBy(m => m.FechaIngreso)
            .Select(m => new MiembroResponse(m.UsuarioId, m.Usuario.Nombre, m.EsAdministrador))
            .ToList());
}
