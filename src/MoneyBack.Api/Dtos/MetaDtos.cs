using MoneyBack.Api.Models.Metas;

namespace MoneyBack.Api.Dtos;

public record CrearMetaRequest(
    string Nombre,
    decimal MontoObjetivo,
    string Icono = "🎯",
    bool EsFondoEmergencia = false,
    decimal PorcentajeRedondeo = 0);

public record ActualizarMetaRequest(
    string Nombre,
    decimal MontoObjetivo,
    string Icono,
    decimal PorcentajeRedondeo);

public record MetaResponse(
    int Id,
    int HogarId,
    string Nombre,
    string Icono,
    bool EsFondoEmergencia,
    decimal PorcentajeRedondeo,
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
    string Nombre,
    string Icono,
    bool EsFondoEmergencia,
    decimal PorcentajeRedondeo,
    decimal MontoObjetivo,
    decimal MontoActual,
    decimal PorcentajeCompletado,
    DateTime? FechaObjetivoEstimada,
    bool Activa,
    DateTime FechaCreacion,
    IReadOnlyList<AportePorUsuario> AportesPorUsuario,
    IReadOnlyList<MovimientoResponse> Movimientos);
