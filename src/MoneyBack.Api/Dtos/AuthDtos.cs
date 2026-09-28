namespace MoneyBack.Api.Dtos;

public record RegistrarUsuarioRequest(string Nombre, string Email, string Password, string CodigoInvitacion);

public record LoginRequest(string Email, string Password);

public record RefrescarTokenRequest(string RefreshToken);

public record AuthResponse(string AccessToken, string RefreshToken, DateTime AccessTokenExpiraEn);

public record PerfilResponse(int Id, string Nombre, string Email, IReadOnlyList<string> Roles, int? DiaPago1, int? DiaPago2);

public record ActualizarPerfilRequest(string Nombre);

public record ActualizarDiasPagoRequest(int? DiaPago1, int? DiaPago2);

public record CambiarPasswordRequest(string PasswordActual, string PasswordNueva);

public record RotarCodigoInvitacionRequest(string NuevoCodigo);

public record UsuarioAdminResponse(int Id, string Nombre, string Email, DateTime FechaCreacion, IReadOnlyList<string> Roles);

/// <param name="Clave">En claro una sola vez: después solo queda el hash.</param>
public record ClaveTemporalResponse(string Clave, int SesionesCerradas);

public record InvitacionAppResponse(
    int Id, string Estado, DateTime CreadoEn, DateTime ExpiraEn, string? UsadaPorNombre);

/// <param name="Token">
/// En claro y una sola vez: de acá en adelante solo queda su hash, así que ni
/// el servidor puede volver a armar el enlace.
/// </param>
public record InvitacionAppCreadaResponse(int Id, string Token, DateTime ExpiraEn);

/// <param name="Codigos">
/// En claro y una sola vez. Después solo queda el hash, así que ni el
/// servidor puede volver a mostrarlos: o se guardan ahora, o hay que generar
/// una lista nueva.
/// </param>
public record CodigosRecuperacionResponse(List<string> Codigos);

public record CodigosRestantesResponse(int Quedan);

public record RecuperarCuentaRequest(string Email, string Codigo, string NuevaPassword);

public record RecuperacionExitosaResponse(int CodigosQueQuedan);
