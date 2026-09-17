using MoneyBack.Api.Models.Metas;

namespace MoneyBack.Api.Dtos;

public record CrearMetaRequest(TipoMeta Tipo, string Nombre, decimal MontoObjetivo);

public record MetaResponse(
    int Id,
    int HogarId,
    TipoMeta Tipo,
    string Nombre,
    decimal MontoObjetivo,
    decimal MontoActual,
    decimal PorcentajeCompletado,
    DateTime? FechaObjetivoEstimada,
    bool Activa,
    DateTime FechaCreacion);

public record CrearMovimientoRequest(
    TipoMovimiento Tipo,
    decimal Monto,
    string? Nota,
    bool EsAutomatico = false);

public record MovimientoResponse(
    int Id,
    int UsuarioId,
    string UsuarioNombre,
    TipoMovimiento Tipo,
    decimal Monto,
    DateTime Fecha,
    string? Nota,
    bool EsAutomatico);

public record AportePorUsuario(int UsuarioId, string UsuarioNombre, decimal TotalAportado);

public record MetaDetalleResponse(
    int Id,
    int HogarId,
    TipoMeta Tipo,
    string Nombre,
    decimal MontoObjetivo,
    decimal MontoActual,
    decimal PorcentajeCompletado,
    DateTime? FechaObjetivoEstimada,
    bool Activa,
    DateTime FechaCreacion,
    IReadOnlyList<AportePorUsuario> AportesPorUsuario,
    IReadOnlyList<MovimientoResponse> Movimientos);
