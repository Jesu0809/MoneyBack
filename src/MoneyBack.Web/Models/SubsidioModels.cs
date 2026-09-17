using System.Text.Json.Serialization;

namespace MoneyBack.Web.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TipoTopeVis { General, Decreto1467, RenovacionUrbana, Vip }

public record SimularSubsidiosRequest(
    decimal IngresoCombinadoMensual,
    decimal ValorVivienda,
    bool EsVip,
    bool CumpleSisbenIvAD20,
    decimal MontoSubsidioCajaCompensacion = 0);

public record AlertaMetaApartamento(int MetaId, string Nombre, decimal MontoObjetivo, bool SuperaTopeVis);

public record SimulacionSubsidiosResponse(
    decimal SmmlvVigente,
    TipoTopeVis TipoTopeVis,
    decimal TopeVisPesos,
    decimal ValorVivienda,
    bool EsVis,
    decimal IngresoCombinadoMensual,
    decimal IngresoCombinadoEnSmmlv,
    bool ElegibleMiCasaYa,
    decimal SubsidioMiCasaYaPesos,
    bool SoloCoberturaTasaFrech,
    string? NotaMiCasaYa,
    decimal MontoSubsidioCajaCompensacion,
    decimal TotalSubsidiosEstimado,
    AlertaMetaApartamento? MetaApartamento);
