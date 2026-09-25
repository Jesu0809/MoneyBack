using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MoneyBack.Api.Data;
using MoneyBack.Api.Dtos;
using MoneyBack.Api.Services;

namespace MoneyBack.Api.Endpoints;

/// <summary>
/// Deja ver y deshacer lo que la app aprendió sobre dónde va cada comercio.
///
/// Existe porque un aprendizaje que no se puede revisar ni corregir es una
/// caja negra: si una vez se clasifica mal un sitio, todos los gastos futuros
/// de ahí entran mal y la única salida sería adivinar que hay que volver a
/// editar un gasto de ese comercio. Poder abrir la lista y borrar una línea
/// convierte eso en algo que se arregla en diez segundos.
/// </summary>
public static class ComerciosAprendidosEndpoints
{
    public static void MapComerciosAprendidosEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/comercios-aprendidos").WithTags("DiaADia").RequireAuthorization();

        group.MapGet("/", async (ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var usuarioId = principal.GetUsuarioId();

            var aprendidos = await db.ComerciosCategoria
                .Where(c => c.UsuarioId == usuarioId)
                .Include(c => c.Categoria)
                .OrderBy(c => c.Comercio)
                .Select(c => new ComercioAprendidoResponse(
                    c.Id, c.Comercio, c.Categoria.Nombre, c.Categoria.Icono, c.FechaAprendido))
                .ToListAsync();

            return Results.Ok(aprendidos);
        });

        group.MapDelete("/{id:int}", async (int id, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var usuarioId = principal.GetUsuarioId();

            var aprendido = await db.ComerciosCategoria
                .FirstOrDefaultAsync(c => c.Id == id && c.UsuarioId == usuarioId);

            if (aprendido is null) return Results.NotFound();

            // Solo se olvida la regla. Los gastos que ya entraron con esa
            // categoría se quedan como están: la persona pidió que la app deje
            // de suponer, no que le reescriba el historial.
            db.ComerciosCategoria.Remove(aprendido);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}
