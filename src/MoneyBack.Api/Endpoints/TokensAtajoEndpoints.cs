using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MoneyBack.Api.Data;
using MoneyBack.Api.Dtos;
using MoneyBack.Api.Models.Auth;
using MoneyBack.Api.Services;

namespace MoneyBack.Api.Endpoints;

/// <summary>
/// Gestión de tokens de Atajos (crear/listar/revocar) — requiere sesión
/// normal (JWT), a diferencia de Endpoints/AtajosEndpoints.cs, que es lo que
/// el Atajo de iOS realmente llama usando el token generado aquí.
/// </summary>
public static class TokensAtajoEndpoints
{
    public static void MapTokensAtajoEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tokens-atajo").WithTags("TokensAtajo").RequireAuthorization();

        group.MapGet("/", async (ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var usuarioId = principal.GetUsuarioId();
            var tokens = await db.TokensAtajo
                .Where(t => t.UsuarioId == usuarioId)
                .OrderByDescending(t => t.FechaCreacion)
                .Select(t => new TokenAtajoResponse(t.Id, t.Nombre, t.FechaCreacion, t.UltimoUso))
                .ToListAsync();

            return Results.Ok(tokens);
        });

        // El historial de llamadas: la única evidencia de si la automatización
        // llegó o no. iOS no muestra un registro de automatizaciones, así que
        // sin esto "el atajo no hizo nada" no se puede resolver, solo adivinar.
        group.MapGet("/llamadas", async (ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var usuarioId = principal.GetUsuarioId();

            var llamadas = await db.LlamadasAtajo
                .Where(l => l.UsuarioId == usuarioId)
                .OrderByDescending(l => l.Fecha)
                .Take(LlamadaAtajo.MaximoPorUsuario)
                .Select(l => new LlamadaAtajoResponse(l.Fecha, l.Texto, l.Exito, l.Detalle))
                .ToListAsync();

            return Results.Ok(llamadas);
        });

        group.MapPost("/", async (CrearTokenAtajoRequest request, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var bytesAleatorios = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
            var tokenEnClaro = "mb_" + Convert.ToBase64String(bytesAleatorios)
                .Replace("+", "").Replace("/", "").Replace("=", "");

            var token = new TokenAtajo
            {
                UsuarioId = principal.GetUsuarioId(),
                TokenHash = TokenService.HashearToken(tokenEnClaro),
                Nombre = request.Nombre
            };
            db.TokensAtajo.Add(token);
            await db.SaveChangesAsync();

            return Results.Created($"/api/tokens-atajo/{token.Id}", new TokenAtajoCreadoResponse(token.Id, tokenEnClaro));
        });

        group.MapDelete("/{id:int}", async (int id, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var usuarioId = principal.GetUsuarioId();
            var token = await db.TokensAtajo.FirstOrDefaultAsync(t => t.Id == id && t.UsuarioId == usuarioId);
            if (token is null) return Results.NotFound();

            db.TokensAtajo.Remove(token);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}
