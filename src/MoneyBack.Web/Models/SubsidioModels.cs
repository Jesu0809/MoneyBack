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
    bool AfiliadoCajaCompensacion,
    bool ViveEnBogota,
    decimal Cesantias,
    bool EsRenovacionUrbana,
    int? AnioEscrituracion);

public record AyudaDisponible(string Nombre, decimal Monto, bool EsSeguro, string Detalle);

public record OpcionCredito(
    string Entidad,
    decimal TasaEfectivaAnual,
    decimal TasaConCobertura,
    bool TieneCobertura,
    int PlazoMeses,
    decimal MontoAFinanciar,
    decimal CuotaInicialNecesaria,
    decimal CuotaMensual,
    decimal CuotaDespuesDeLaCobertura,
    decimal IngresoMinimoRequerido,
    decimal ProporcionDelIngreso,
    bool CabeEnElIngreso,
    bool CabeCuandoSubaLaCuota,
    decimal LeFaltaParaLaCuotaInicial,
    string Nota);

public record PlanViviendaResponse(
    decimal ValorVivienda,
    string Clasificacion,
    decimal TopeVis,
    decimal TopeVip,
    decimal IngresoEnSmmlv,
    int AnioEscrituracion,
    List<AyudaDisponible> Ayudas,
    decimal TotalSubsidiosSeguros,
    List<OpcionCredito> Opciones,
    decimal AhorroActual,
    decimal Cesantias,
    decimal DisponibleParaCuotaInicial,
    ComparacionVis? SiFueraVis,
    TechoDeCompra Techo,
    AlertaUmbral? Umbral,
    DateOnly DatosVigentesDesde);

public record TechoDeCompra(decimal PrecioMaximo, bool LimitadoPorElTopeVis, decimal CuotaEstimada);

public record AlertaUmbral(
    decimal IngresoDelUmbral, decimal SeExcedenPor, decimal SubsidiosSiEstuvieranDebajo);

public record ComparacionVis(
    decimal ValorMaximoVis,
    decimal SubsidiosQuePerdieron,
    decimal CuotaMensual,
    decimal IngresoMinimoRequerido);
