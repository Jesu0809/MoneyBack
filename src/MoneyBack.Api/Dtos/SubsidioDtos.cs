using MoneyBack.Api.Models.Subsidios;

namespace MoneyBack.Api.Dtos;

public record SimularSubsidiosRequest(
    decimal IngresoCombinadoMensual,
    decimal ValorVivienda,
    bool EsVip,
    /// <summary>
    /// Requisito de Mi Casa Ya que el simulador no puede verificar por su
    /// cuenta: el usuario confirma manualmente que está en Sisbén IV A1-D20.
    /// </summary>
    bool CumpleSisbenIvAD20,
    /// <summary>
    /// Monto del subsidio de caja de compensación (Colsubsidio u otra). No se
    /// calcula: cada caja publica su propia tabla y cambia con frecuencia, así
    /// que el usuario lo ingresa manualmente tras consultar su portal transaccional.
    /// </summary>
    decimal MontoSubsidioCajaCompensacion = 0);

public record AlertaMetaApartamento(
    int MetaId,
    string Nombre,
    decimal MontoObjetivo,
    bool SuperaTopeVis);

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
