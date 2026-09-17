namespace MoneyBack.Api.Dtos;

public record CrearHogarRequest(
    int UsuarioParejaId,
    bool AplicaTope150 = false,
    decimal PorcentajeRedondeoEmergencia = 20,
    decimal PorcentajeRedondeoApartamento = 80);

public record ActualizarHogarRequest(
    bool AplicaTope150,
    decimal PorcentajeRedondeoEmergencia,
    decimal PorcentajeRedondeoApartamento);

public record HogarResponse(
    int Id,
    int Usuario1Id,
    int Usuario2Id,
    bool AplicaTope150,
    decimal PorcentajeRedondeoEmergencia,
    decimal PorcentajeRedondeoApartamento);
