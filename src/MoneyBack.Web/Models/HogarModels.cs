namespace MoneyBack.Web.Models;

public record CrearHogarRequest(
    string EmailPareja,
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
    string Usuario1Nombre,
    int Usuario2Id,
    string Usuario2Nombre,
    bool AplicaTope150,
    decimal PorcentajeRedondeoEmergencia,
    decimal PorcentajeRedondeoApartamento);
