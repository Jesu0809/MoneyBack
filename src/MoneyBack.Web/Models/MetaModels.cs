using System.Text.Json.Serialization;

namespace MoneyBack.Web.Models;

// TipoMeta se quitó: la gente ahorra para una lavadora, un viaje o la
// matrícula, y un enum de dos valores obligaba a que todo eso fuera
// "Apartamento". Ahora la meta lleva nombre e ícono libres.

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TipoMovimiento { Aporte, Retiro }

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

public record CrearMovimientoRequest(TipoMovimiento Tipo, decimal Monto, string? Nota, bool EsAutomatico = false);

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
    List<AportePorUsuario> AportesPorUsuario,
    List<MovimientoResponse> Movimientos);

public record DestinoAporteResponse(
    string Caso, int? MetaId, string? MetaNombre, string? MetaIcono, int MetasActivas);
