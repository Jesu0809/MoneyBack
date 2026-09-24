using System.Text.Json.Serialization;

namespace MoneyBack.Web.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TipoCategoria { Ingreso, Gasto }

public record CategoriaResponse(int Id, string Nombre, TipoCategoria Tipo, string Icono, bool Activa);
public record CrearCategoriaRequest(string Nombre, TipoCategoria Tipo, string Icono);
public record ActualizarCategoriaRequest(string Nombre, string Icono, bool Activa);

public record ActualizarMovimientoDiaADiaRequest(int CategoriaId, decimal Monto, DateTime? Fecha, string? Nota, int? TarjetaCreditoId = null);

/// <summary>
/// Estado del formulario de edición. Vive aquí y no dentro de una página
/// porque el editor se usa en dos lugares: la lista del día a día y la
/// sección de gastos sin clasificar.
/// </summary>
public class EdicionMovimientoModel
{
    public int CategoriaId { get; set; }
    public decimal? Monto { get; set; }
    public string? Nota { get; set; }
    public int? TarjetaCreditoId { get; set; }
}

public record CrearMovimientoDiaADiaRequest(int CategoriaId, decimal Monto, DateTime? Fecha, string? Nota, int? TarjetaCreditoId = null);

public record MovimientoDiaADiaResponse(
    int Id,
    int CategoriaId,
    string CategoriaNombre,
    string CategoriaIcono,
    TipoCategoria Tipo,
    decimal Monto,
    DateTime Fecha,
    string? Nota,
    bool RedondeoAplicado,
    int? TarjetaCreditoId,
    string? TarjetaCreditoNombre);

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
    int Anio);
