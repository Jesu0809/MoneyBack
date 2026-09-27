using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MoneyBack.Api.Config;
using MoneyBack.Api.Data;
using MoneyBack.Api.Domain.Subsidios;
using MoneyBack.Api.Dtos;
using MoneyBack.Api.Models.Metas;
using MoneyBack.Api.Models.Subsidios;
using MoneyBack.Api.Services;

namespace MoneyBack.Api.Endpoints;

public static class SubsidiosEndpoints
{
    public static void MapSubsidiosEndpoints(this WebApplication app)
    {
        // El simulador anterior le pedía a la persona el monto de su subsidio
        // y se lo devolvía sumado: le preguntaba justo la respuesta que debía
        // darle. Acá lo único que no se puede saber es cuánto vale la vivienda
        // que está mirando y cuánto gana el hogar; el resto se calcula con
        // cifras reales del mercado (ver ParametrosVivienda).
        app.MapPost("/api/hogares/{hogarId:int}/plan-vivienda", async (
            int hogarId,
            PlanViviendaRequest request,
            ClaimsPrincipal principal,
            ApplicationDbContext db,
            IOptions<SubsidiosOptions> opciones) =>
        {
            var smmlv = opciones.Value.SmmlvVigente;
            if (smmlv <= 0)
            {
                return Results.Problem("El SMMLV vigente no está configurado (Subsidios:SmmlvVigente).", statusCode: 500);
            }

            if (request.IngresoCombinadoMensual <= 0 || request.ValorVivienda <= 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["montos"] = ["El ingreso del hogar y el valor de la vivienda deben ser mayores a cero."]
                });
            }

            var hogar = await db.Hogares.FindAsync(hogarId);
            if (hogar is null) return Results.NotFound($"No existe el hogar {hogarId}.");
            if (!hogar.PerteneceAlHogar(principal)) return Results.Forbid();

            // Lo que ya llevan ahorrado sale de sus propias metas, no de una
            // pregunta: la app lo sabe, y preguntarlo sería repetir el error
            // del simulador viejo.
            // Todo lo ahorrado menos el fondo de emergencia: ese existe
            // justamente para NO gastarlo en la cuota inicial.
            var ahorroActual = await db.MetasAhorro
                .Where(m => m.HogarId == hogarId && m.Activa && !m.EsFondoEmergencia)
                .SelectMany(m => m.Movimientos)
                .SumAsync(mv => (decimal?)mv.Monto) ?? 0m;

            var tipoTope = hogar.AplicaTope150 ? TipoTopeVis.Decreto1467 : TipoTopeVis.General;

            var plan = PlanVivienda.Calcular(
                request.ValorVivienda,
                request.IngresoCombinadoMensual,
                smmlv,
                tipoTope,
                request.AfiliadoCajaCompensacion,
                request.ViveEnBogota,
                Math.Max(0, request.Cesantias),
                ahorroActual,
                request.EsRenovacionUrbana,
                request.AnioEscrituracion);

            return Results.Ok(plan);
        }).RequireAuthorization();

        app.MapPost("/api/hogares/{hogarId:int}/subsidios/simular", async (
            int hogarId,
            SimularSubsidiosRequest request,
            ClaimsPrincipal principal,
            ApplicationDbContext db,
            IOptions<SubsidiosOptions> opciones) =>
        {
            var smmlv = opciones.Value.SmmlvVigente;
            if (smmlv <= 0)
            {
                return Results.Problem("El SMMLV vigente no está configurado (Subsidios:SmmlvVigente).", statusCode: 500);
            }

            if (request.IngresoCombinadoMensual <= 0 || request.ValorVivienda <= 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["montos"] = ["IngresoCombinadoMensual y ValorVivienda deben ser mayores a cero."]
                });
            }

            var hogar = await db.Hogares.FindAsync(hogarId);
            if (hogar is null) return Results.NotFound($"No existe el hogar {hogarId}.");
            if (!hogar.PerteneceAlHogar(principal)) return Results.Forbid();

            var tipoTopeVis = request.EsVip
                ? TipoTopeVis.Vip
                : (hogar.AplicaTope150 ? TipoTopeVis.Decreto1467 : TipoTopeVis.General);

            var topeVisPesos = TopesVis.TopeEnPesos(tipoTopeVis, smmlv);
            var esVis = request.ValorVivienda <= topeVisPesos;

            var notaProgramaGobierno = esVis
                ? "Mi Casa Ya no tiene presupuesto para 2026 y no está recibiendo postulaciones nuevas. " +
                  "Su reemplazo, Mi Casa Milagro, se está estructurando desde agosto de 2026 pero el Gobierno " +
                  "todavía no publica montos ni requisitos oficiales — por eso no calculamos una cifra de este subsidio."
                : "La vivienda supera el tope VIS para esta ubicación, así que no aplicaría a ningún subsidio de vivienda de interés social.";

            var montoCaja = Math.Max(0, request.MontoSubsidioCajaCompensacion);
            var totalEstimado = montoCaja;

            var metaApartamento = await db.MetasAhorro
                .Where(m => m.HogarId == hogarId && !m.EsFondoEmergencia && m.Activa)
                .OrderByDescending(m => m.FechaCreacion)
                .Select(m => new { m.Id, m.Nombre, m.MontoObjetivo })
                .FirstOrDefaultAsync();

            var alertaMeta = metaApartamento is null
                ? null
                : new AlertaMetaApartamento(
                    metaApartamento.Id,
                    metaApartamento.Nombre,
                    metaApartamento.MontoObjetivo,
                    metaApartamento.MontoObjetivo > topeVisPesos);

            var response = new SimulacionSubsidiosResponse(
                smmlv,
                tipoTopeVis,
                topeVisPesos,
                request.ValorVivienda,
                esVis,
                request.IngresoCombinadoMensual,
                Math.Round(request.IngresoCombinadoMensual / smmlv, 2),
                notaProgramaGobierno,
                montoCaja,
                totalEstimado,
                alertaMeta);

            return Results.Ok(response);
        })
        .WithTags("Subsidios")
        .RequireAuthorization();
    }
}
