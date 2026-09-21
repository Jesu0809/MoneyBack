namespace MoneyBack.Api.Dtos;

public record CrearTarjetaCreditoRequest(string Nombre, int DiaCorte);

public record ActualizarTarjetaCreditoRequest(string Nombre, int DiaCorte, bool Activa);

public record TarjetaCreditoResponse(int Id, string Nombre, int DiaCorte, bool Activa);

public record SaldoPendienteResponse(decimal Monto, DateTime? UltimoPago);

public record RegistrarPagoTarjetaRequest(decimal Monto);
