namespace MoneyBack.Api.Dtos;

public record CrearHogarRequest(
    int Usuario1Id,
    int Usuario2Id,
    decimal PorcentajeRedondeoEmergencia = 20,
    decimal PorcentajeRedondeoApartamento = 80);

public record HogarResponse(
    int Id,
    int Usuario1Id,
    int Usuario2Id,
    decimal PorcentajeRedondeoEmergencia,
    decimal PorcentajeRedondeoApartamento);
