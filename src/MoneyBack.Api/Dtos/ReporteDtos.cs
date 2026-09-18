namespace MoneyBack.Api.Dtos;

public record TotalPorMes(int Mes, decimal TotalIngresos, decimal TotalGastos);

public record ResumenAnualResponse(
    int Anio,
    decimal TotalIngresos,
    decimal TotalGastos,
    List<TotalPorMes> PorMes,
    List<TotalPorCategoria> GastosPorCategoria);
