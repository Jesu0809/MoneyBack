using MoneyBack.Api.Models.Metas;

namespace MoneyBack.Api.Dtos;

public record CrearInvitacionHogarRequest(
    string EmailPareja,
    bool AplicaTope150 = false);

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
    bool RedondeoActivo);

public record HogarResponse(
    int Id,
    int Usuario1Id,
    string Usuario1Nombre,
    int Usuario2Id,
    string Usuario2Nombre,
    bool AplicaTope150,
    bool RedondeoActivo);

public record RedondeoTotalResponse(decimal Total);
