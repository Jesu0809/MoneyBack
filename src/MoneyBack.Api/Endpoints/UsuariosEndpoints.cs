using Microsoft.EntityFrameworkCore;
using MoneyBack.Api.Data;
using MoneyBack.Api.Dtos;
using MoneyBack.Api.Models;

namespace MoneyBack.Api.Endpoints;

public static class UsuariosEndpoints
{
    public static void MapUsuariosEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/usuarios").WithTags("Usuarios");

        group.MapPost("/", async (CrearUsuarioRequest request, ApplicationDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(request.Nombre) || string.IsNullOrWhiteSpace(request.Email))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["nombre_email"] = ["Nombre y Email son obligatorios."]
                });
            }

            var usuario = new Usuario { Nombre = request.Nombre, Email = request.Email };
            db.Usuarios.Add(usuario);
            await db.SaveChangesAsync();

            var response = new UsuarioResponse(usuario.Id, usuario.Nombre, usuario.Email);
            return Results.Created($"/api/usuarios/{usuario.Id}", response);
        });

        group.MapGet("/{id:int}", async (int id, ApplicationDbContext db) =>
        {
            var usuario = await db.Usuarios.FindAsync(id);
            if (usuario is null) return Results.NotFound();

            return Results.Ok(new UsuarioResponse(usuario.Id, usuario.Nombre, usuario.Email));
        });
    }
}
