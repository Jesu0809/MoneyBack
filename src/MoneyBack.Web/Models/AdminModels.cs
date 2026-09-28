namespace MoneyBack.Web.Models;

public record UsuarioAdminResponse(int Id, string Nombre, string Email, DateTime FechaCreacion, List<string> Roles);

/// <param name="Clave">Se muestra una sola vez: el servidor solo guarda su hash.</param>
public record ClaveTemporalResponse(string Clave, int SesionesCerradas);

public record RotarCodigoInvitacionRequest(string NuevoCodigo);

public record CodigoInvitacionInfo(int Id, DateTime CreadoEn);
