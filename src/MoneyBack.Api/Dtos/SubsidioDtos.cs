using MoneyBack.Api.Models.Subsidios;

namespace MoneyBack.Api.Dtos;

public record SimularSubsidiosRequest(
    decimal IngresoCombinadoMensual,
    decimal ValorVivienda,
    bool EsVip,
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
    /// <summary>
    /// Mi Casa Ya se descontinuó (sin presupuesto para 2026); su reemplazo,
    /// Mi Casa Milagro, todavía no publica reglas ni montos oficiales — así
    /// que en vez de inventar una cifra, esto es solo un mensaje de estado.
    /// </summary>
    string NotaProgramaGobierno,
    decimal MontoSubsidioCajaCompensacion,
    decimal TotalSubsidiosEstimado,
    AlertaMetaApartamento? MetaApartamento);
