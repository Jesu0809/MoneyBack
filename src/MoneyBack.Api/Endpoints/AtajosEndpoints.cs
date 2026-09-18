using Microsoft.EntityFrameworkCore;
using MoneyBack.Api.Data;
using MoneyBack.Api.Dtos;
using MoneyBack.Api.Models.DiaADia;
using MoneyBack.Api.Services;

namespace MoneyBack.Api.Endpoints;

/// <summary>
/// Endpoints pensados para que un Atajo de iOS los llame directo (acción
/// "Obtener contenido de URL"), sin pasar por la interfaz ni por el login
/// normal. Deliberadamente NO usan JWT ni RequireAuthorization() — se
/// autentican con el header X-Atajo-Token (ver TokensAtajoEndpoints.cs para
/// cómo se genera) y están limitados a leer categorías y crear un
/// movimiento, nada más: ni con el token en mano se puede tocar metas,
/// deudas, ni el resto de la cuenta.
/// </summary>
public static class AtajosEndpoints
{
    public static void MapAtajosEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/atajos").WithTags("Atajos");

        group.MapGet("/categorias", async (HttpRequest request, TipoCategoria? tipo, ApplicationDbContext db) =>
        {
            var usuarioId = await ValidarTokenAsync(request, db);
            if (usuarioId is null) return Results.Unauthorized();

            var query = db.Categorias.Where(c => c.UsuarioId == usuarioId && c.Activa);
            if (tipo is not null) query = query.Where(c => c.Tipo == tipo);

            var categorias = await query
                .OrderBy(c => c.Nombre)
                .Select(c => new CategoriaAtajoResponse(c.Id, c.Nombre, c.Icono, c.Tipo))
                .ToListAsync();

            return Results.Ok(categorias);
        });

        group.MapPost("/movimientos", async (HttpRequest request, CrearMovimientoAtajoRequest body, ApplicationDbContext db) =>
        {
            var usuarioId = await ValidarTokenAsync(request, db);
            if (usuarioId is null) return Results.Unauthorized();

            if (body.Monto <= 0) return Results.BadRequest(new { error = "El monto debe ser mayor a cero." });

            var categoria = await db.Categorias.FirstOrDefaultAsync(c => c.Id == body.CategoriaId && c.UsuarioId == usuarioId.Value);
            if (categoria is null) return Results.BadRequest(new { error = "La categoría no existe." });

            var movimiento = new MovimientoDiaADia
            {
                UsuarioId = usuarioId.Value,
                CategoriaId = categoria.Id,
                Monto = body.Monto,
                Nota = body.Nota
            };
            db.MovimientosDiaADia.Add(movimiento);

            if (categoria.Tipo == TipoCategoria.Gasto)
            {
                await RedondeoService.AplicarSiCorrespondeAsync(movimiento, db);
            }

            await db.SaveChangesAsync();

            return Results.Created($"/api/movimientos-diaadia/{movimiento.Id}", new { movimiento.Id });
        });

        // Variante por nombre de categoría (en vez de Id) con todo en query
        // string: existe específicamente para que el .shortcut generado en
        // Perfil pueda armar la URL con una sola acción "Texto" concatenando
        // las respuestas de "Preguntar", sin tener que construir un cuerpo
        // JSON dentro del archivo del Atajo (mucho más simple y menos
        // propenso a errores en el formato del plist que genera el botón
        // "Descargar Atajo").
        group.MapPost("/movimientos-por-nombre", async (HttpRequest request, string categoriaNombre, decimal monto, ApplicationDbContext db) =>
        {
            var usuarioId = await ValidarTokenAsync(request, db);
            if (usuarioId is null) return Results.Unauthorized();

            if (monto <= 0) return Results.BadRequest(new { error = "El monto debe ser mayor a cero." });

            var categoria = await db.Categorias
                .Where(c => c.UsuarioId == usuarioId.Value && c.Activa)
                .Where(c => c.Nombre.ToLower() == categoriaNombre.ToLower())
                .OrderBy(c => c.Id)
                .FirstOrDefaultAsync();

            if (categoria is null)
            {
                return Results.BadRequest(new { error = $"No se encontró la categoría \"{categoriaNombre}\"." });
            }

            var movimiento = new MovimientoDiaADia
            {
                UsuarioId = usuarioId.Value,
                CategoriaId = categoria.Id,
                Monto = monto
            };
            db.MovimientosDiaADia.Add(movimiento);

            if (categoria.Tipo == TipoCategoria.Gasto)
            {
                await RedondeoService.AplicarSiCorrespondeAsync(movimiento, db);
            }

            await db.SaveChangesAsync();

            return Results.Ok(new { mensaje = $"{(categoria.Tipo == TipoCategoria.Gasto ? "Gasto" : "Ingreso")} de {monto:N0} en {categoria.Nombre} registrado." });
        });
    }

    private static async Task<int?> ValidarTokenAsync(HttpRequest request, ApplicationDbContext db)
    {
        if (!request.Headers.TryGetValue("X-Atajo-Token", out var valores)) return null;

        var tokenEnClaro = valores.ToString();
        if (string.IsNullOrWhiteSpace(tokenEnClaro)) return null;

        var hash = TokenService.HashearToken(tokenEnClaro);
        var token = await db.TokensAtajo.FirstOrDefaultAsync(t => t.TokenHash == hash);
        if (token is null) return null;

        token.UltimoUso = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return token.UsuarioId;
    }
}
