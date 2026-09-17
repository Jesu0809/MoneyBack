using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MoneyBack.Api.Data;
using MoneyBack.Api.Dtos;
using MoneyBack.Api.Models.DiaADia;
using MoneyBack.Api.Services;

namespace MoneyBack.Api.Endpoints;

public static class CategoriasEndpoints
{
    public static void MapCategoriasEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/categorias").WithTags("Categorias").RequireAuthorization();

        group.MapGet("/", async (ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var usuarioId = principal.GetUsuarioId();
            var categorias = await db.Categorias
                .Where(c => c.UsuarioId == usuarioId)
                .OrderBy(c => c.Tipo).ThenBy(c => c.Nombre)
                .Select(c => new CategoriaResponse(c.Id, c.Nombre, c.Tipo, c.Icono, c.Activa))
                .ToListAsync();

            return Results.Ok(categorias);
        });

        group.MapPost("/", async (CrearCategoriaRequest request, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(request.Nombre))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["nombre"] = ["El nombre es obligatorio."]
                });
            }

            var categoria = new Categoria
            {
                UsuarioId = principal.GetUsuarioId(),
                Nombre = request.Nombre.Trim(),
                Tipo = request.Tipo,
                Icono = string.IsNullOrWhiteSpace(request.Icono) ? "📦" : request.Icono
            };
            db.Categorias.Add(categoria);
            await db.SaveChangesAsync();

            return Results.Created($"/api/categorias/{categoria.Id}",
                new CategoriaResponse(categoria.Id, categoria.Nombre, categoria.Tipo, categoria.Icono, categoria.Activa));
        });

        group.MapPut("/{id:int}", async (int id, ActualizarCategoriaRequest request, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var usuarioId = principal.GetUsuarioId();
            var categoria = await db.Categorias.FirstOrDefaultAsync(c => c.Id == id && c.UsuarioId == usuarioId);
            if (categoria is null) return Results.NotFound();

            if (string.IsNullOrWhiteSpace(request.Nombre))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["nombre"] = ["El nombre es obligatorio."]
                });
            }

            categoria.Nombre = request.Nombre.Trim();
            categoria.Icono = string.IsNullOrWhiteSpace(request.Icono) ? categoria.Icono : request.Icono;
            categoria.Activa = request.Activa;
            await db.SaveChangesAsync();

            return Results.Ok(new CategoriaResponse(categoria.Id, categoria.Nombre, categoria.Tipo, categoria.Icono, categoria.Activa));
        });

        group.MapDelete("/{id:int}", async (int id, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var usuarioId = principal.GetUsuarioId();
            var categoria = await db.Categorias.FirstOrDefaultAsync(c => c.Id == id && c.UsuarioId == usuarioId);
            if (categoria is null) return Results.NotFound();

            var tieneMovimientos = await db.MovimientosDiaADia.AnyAsync(m => m.CategoriaId == id);
            if (tieneMovimientos)
            {
                // No se puede borrar sin romper el historial: se archiva en su lugar.
                categoria.Activa = false;
                await db.SaveChangesAsync();
                return Results.Ok(new { archivada = true });
            }

            db.Categorias.Remove(categoria);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}
