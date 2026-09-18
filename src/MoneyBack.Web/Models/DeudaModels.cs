using System.Text.Json.Serialization;

namespace MoneyBack.Web.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TipoPropiedadDeuda { Privada, Compartida }

public record CrearDeudaPrivadaRequest(string Nombre, decimal MontoTotal, decimal MontoCuota, int TotalCuotas);

public record CrearDeudaCompartidaRequest(string Nombre, decimal MontoTotal, decimal MontoCuota, int TotalCuotas);

public record DeudaResponse(
    int Id, TipoPropiedadDeuda TipoPropiedad, string Nombre,
    decimal MontoTotal, decimal MontoCuota, int TotalCuotas, int CuotasPagadas,
    decimal MontoRestante, decimal PorcentajePagado, bool Activa);

public record PagarCuotaRequest(int CategoriaId, string? Nota);
