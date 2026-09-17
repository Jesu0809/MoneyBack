using Microsoft.Extensions.Options;
using MoneyBack.Api.Config;
using MoneyBack.Api.Domain.Subsidios;
using MoneyBack.Api.Dtos;

namespace MoneyBack.Api.Endpoints;

public static class SubsidiosEndpoints
{
    public static void MapSubsidiosEndpoints(this WebApplication app)
    {
        app.MapPost("/api/subsidios/simular", (SimularSubsidiosRequest request, IOptions<SubsidiosOptions> opciones) =>
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

            var topeVisPesos = TopesVis.TopeEnPesos(request.TipoTopeVis, smmlv);
            var esVis = request.ValorVivienda <= topeVisPesos;

            var miCasaYa = esVis
                ? MiCasaYaCalculator.Calcular(request.IngresoCombinadoMensual, smmlv, request.CumpleSisbenIvAD20)
                : new ResultadoMiCasaYa(false, 0, 0, false,
                    "La vivienda supera el tope VIS para esta ubicación: Mi Casa Ya solo aplica a vivienda VIS.");

            var montoCaja = Math.Max(0, request.MontoSubsidioCajaCompensacion);
            var totalEstimado = miCasaYa.SubsidioEnPesos + montoCaja;

            var response = new SimulacionSubsidiosResponse(
                smmlv,
                request.TipoTopeVis,
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
                totalEstimado);

            return Results.Ok(response);
        })
        .WithTags("Subsidios");
    }
}
