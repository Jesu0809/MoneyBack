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

public record PlanViviendaRequest(
    decimal ValorVivienda,
    decimal IngresoCombinadoMensual,
    bool AfiliadoCajaCompensacion);

public record AyudaDisponible(string Nombre, decimal Monto, bool EsSeguro, string Detalle);

public record OpcionCredito(
    string Entidad,
    decimal TasaEfectivaAnual,
    decimal MontoAFinanciar,
    decimal CuotaInicialNecesaria,
    decimal CuotaMensual,
    decimal ProporcionDelIngreso,
    bool CabeEnElIngreso,
    decimal LeFaltaParaLaCuotaInicial,
    string Nota);

public record PlanViviendaResponse(
    decimal ValorVivienda,
    string Clasificacion,
    decimal TopeVis,
    decimal TopeVip,
    decimal IngresoEnSmmlv,
    List<AyudaDisponible> Subsidios,
    decimal TotalSubsidiosSeguros,
    List<OpcionCredito> Opciones,
    decimal AhorroActual,
    DateOnly DatosVigentesDesde);
