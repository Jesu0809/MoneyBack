using System.Text.Json.Serialization;

namespace MoneyBack.Web.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EstadoInvitacionHogar { Pendiente, Aceptada, Rechazada }

public record CrearInvitacionHogarRequest(string EmailPareja, int? HogarId = null);

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

public record CrearHogarRequest(string Nombre, bool AplicaTope150 = false);

public record ActualizarGrupoRequest(string Nombre, bool AplicaTope150, bool RedondeoActivo);

public record MiembroResponse(int UsuarioId, string Nombre, bool EsAdministrador);

public record HogarResponse(
    int Id,
    string Nombre,
    bool AplicaTope150,
    bool RedondeoActivo,
    bool SoyAdministrador,
    List<MiembroResponse> Miembros);

public record RedondeoTotalResponse(decimal Total);
