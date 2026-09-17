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

            var miCasaYa = esVis
                ? MiCasaYaCalculator.Calcular(request.IngresoCombinadoMensual, smmlv, request.CumpleSisbenIvAD20)
                : new ResultadoMiCasaYa(false, 0, 0, false,
                    "La vivienda supera el tope VIS para esta ubicación: Mi Casa Ya solo aplica a vivienda VIS.");

            var montoCaja = Math.Max(0, request.MontoSubsidioCajaCompensacion);
            var totalEstimado = miCasaYa.SubsidioEnPesos + montoCaja;

            var metaApartamento = await db.MetasAhorro
                .Where(m => m.HogarId == hogarId && m.Tipo == TipoMeta.Apartamento && m.Activa)
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
                miCasaYa.Elegible,
                miCasaYa.SubsidioEnPesos,
                miCasaYa.SoloCoberturaTasaFrech,
                miCasaYa.Nota,
                montoCaja,
                totalEstimado,
                alertaMeta);

            return Results.Ok(response);
        })
        .WithTags("Subsidios")
        .RequireAuthorization();
    }
}
