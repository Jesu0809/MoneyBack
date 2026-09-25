using MoneyBack.Api.Models.DiaADia;

namespace MoneyBack.Api.Dtos;

public record CrearCategoriaRequest(string Nombre, TipoCategoria Tipo, string Icono);
public record ActualizarCategoriaRequest(string Nombre, string Icono, bool Activa);

public record CategoriaResponse(int Id, string Nombre, TipoCategoria Tipo, string Icono, bool Activa);

public record CrearMovimientoDiaADiaRequest(int CategoriaId, decimal Monto, DateTime? Fecha, string? Nota, int? TarjetaCreditoId = null);

public record ActualizarMovimientoDiaADiaRequest(int CategoriaId, decimal Monto, DateTime? Fecha, string? Nota, int? TarjetaCreditoId = null);

public record MovimientoDiaADiaResponse(
    int Id,
    int CategoriaId,
    string CategoriaNombre,
    string CategoriaIcono,
    TipoCategoria Tipo,
    decimal Monto,
    DateTime Fecha,
    string? Nota,
    string? Comercio,
    bool RedondeoAplicado,
    int? TarjetaCreditoId,
    string? TarjetaCreditoNombre);

/// <param name="ComercioAprendido">
/// El comercio cuya categoría quedó guardada, o null si esta edición no
/// enseñó nada (no cambió la categoría, o el gasto no trae comercio). Se
/// devuelve en vez de dejar que la app lo deduzca de la nota: deducirlo la
/// llevaba a prometer que había aprendido incluso cuando solo se corrigió el
/// monto, y un aviso que no corresponde a lo que pasó enseña a desconfiar de
/// todos los demás.
/// </param>
/// <param name="OtrosReclasificados">
/// Cuántos gastos anteriores del mismo comercio, que seguían sin clasificar,
/// se movieron también. Se devuelve para poder decírselo a la persona: que la
/// corrección haya servido para varios de una es lo que hace que valga la pena
/// volver a corregir.
/// </param>
public record MovimientoActualizadoResponse(
    MovimientoDiaADiaResponse Movimiento, string? ComercioAprendido, int OtrosReclasificados);

public record TotalPorCategoria(int CategoriaId, string CategoriaNombre, string CategoriaIcono, decimal Total);

public record ResumenDiaADiaResponse(
    decimal TotalIngresos,
    decimal TotalGastos,
    decimal Balance,
    List<TotalPorCategoria> GastosPorCategoria);

public record GuardarPresupuestoRequest(int CategoriaId, decimal MontoLimite, int Mes, int Anio);

public record PresupuestoResponse(
    int Id,
    int CategoriaId,
    string CategoriaNombre,
    string CategoriaIcono,
    decimal MontoLimite,
    decimal MontoGastado,
    int Mes,
    int Anio,
    bool Heredado = false);

/// <param name="Comercio">
/// La llave normalizada (sin tildes ni mayúsculas) con la que se reconoce el
/// sitio. Se muestra tal cual y no una versión "bonita": es exactamente lo que
/// la app compara, así que verla así es lo que permite entender por qué un
/// gasto entró donde entró.
/// </param>
public record ComercioAprendidoResponse(
    int Id, string Comercio, string CategoriaNombre, string CategoriaIcono, DateTime FechaAprendido);
