using MoneyBack.Api.Models.Deudas;

namespace MoneyBack.Api.Dtos;

public record CrearDeudaPrivadaRequest(string Nombre, decimal MontoTotal, decimal MontoCuota, int TotalCuotas);

public record CrearDeudaCompartidaRequest(string Nombre, decimal MontoTotal, decimal MontoCuota, int TotalCuotas);

public record DeudaResponse(
    int Id, TipoPropiedadDeuda TipoPropiedad, string Nombre,
    decimal MontoTotal, decimal MontoCuota, int TotalCuotas, int CuotasPagadas,
    decimal MontoRestante, decimal PorcentajePagado, bool Activa);

public record PagarCuotaRequest(int CategoriaId, string? Nota);

public record PagoDeudaResponse(int Id, int UsuarioId, string UsuarioNombre, decimal Monto, DateTime Fecha);
