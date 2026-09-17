using MoneyBack.Api.Models.DiaADia;

namespace MoneyBack.Api.Dtos;

public record CrearCategoriaRequest(string Nombre, TipoCategoria Tipo, string Icono);
public record ActualizarCategoriaRequest(string Nombre, string Icono, bool Activa);

public record CategoriaResponse(int Id, string Nombre, TipoCategoria Tipo, string Icono, bool Activa);

public record CrearMovimientoDiaADiaRequest(int CategoriaId, decimal Monto, DateTime? Fecha, string? Nota);

public record MovimientoDiaADiaResponse(
    int Id,
    int CategoriaId,
    string CategoriaNombre,
    string CategoriaIcono,
    TipoCategoria Tipo,
    decimal Monto,
    DateTime Fecha,
    string? Nota,
    bool RedondeoAplicado);

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
