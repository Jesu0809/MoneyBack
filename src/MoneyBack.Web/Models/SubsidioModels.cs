using System.Text.Json.Serialization;

namespace MoneyBack.Web.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TipoTopeVis { General, Decreto1467, RenovacionUrbana, Vip }

public record SimularSubsidiosRequest(
    decimal IngresoCombinadoMensual,
    decimal ValorVivienda,
    bool EsVip,
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
    string NotaProgramaGobierno,
    decimal MontoSubsidioCajaCompensacion,
    decimal TotalSubsidiosEstimado,
    AlertaMetaApartamento? MetaApartamento);
