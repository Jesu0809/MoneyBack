using System.Text.Json.Serialization;

namespace MoneyBack.Web.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EstadoInvitacionHogar { Pendiente, Aceptada, Rechazada }

public record CrearInvitacionHogarRequest(
    string EmailPareja,
    bool AplicaTope150 = false,
    decimal PorcentajeRedondeoEmergencia = 20,
    decimal PorcentajeRedondeoApartamento = 80);

public record InvitacionHogarResponse(
    int Id,
    int InvitadorId,
    string InvitadorNombre,
    int InvitadoId,
    string InvitadoNombre,
    EstadoInvitacionHogar Estado,
    DateTime FechaCreacion);

public record MisInvitacionesHogarResponse(
    InvitacionHogarResponse? Recibida,
    InvitacionHogarResponse? Enviada);

public record ActualizarHogarRequest(
    bool AplicaTope150,
    bool RedondeoActivo,
    decimal PorcentajeRedondeoEmergencia,
    decimal PorcentajeRedondeoApartamento);

public record HogarResponse(
    int Id,
    int Usuario1Id,
    string Usuario1Nombre,
    int Usuario2Id,
    string Usuario2Nombre,
    bool AplicaTope150,
    bool RedondeoActivo,
    decimal PorcentajeRedondeoEmergencia,
    decimal PorcentajeRedondeoApartamento);
