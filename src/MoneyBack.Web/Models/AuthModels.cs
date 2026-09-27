namespace MoneyBack.Web.Models;

public record RegistrarUsuarioRequest(string Nombre, string Email, string Password, string CodigoInvitacion);

public record LoginRequest(string Email, string Password);

public record RefrescarTokenRequest(string RefreshToken);

public record AuthResponse(string AccessToken, string RefreshToken, DateTime AccessTokenExpiraEn);

public record PerfilResponse(int Id, string Nombre, string Email, List<string> Roles, int? DiaPago1, int? DiaPago2);

public record ActualizarPerfilRequest(string Nombre);

public record ActualizarDiasPagoRequest(int? DiaPago1, int? DiaPago2);

public record CambiarPasswordRequest(string PasswordActual, string PasswordNueva);

public record InvitacionAppResponse(
    int Id, string Estado, DateTime CreadoEn, DateTime ExpiraEn, string? UsadaPorNombre);

public record InvitacionAppCreadaResponse(int Id, string Token, DateTime ExpiraEn);

public record CodigosRecuperacionResponse(List<string> Codigos);
public record CodigosRestantesResponse(int Quedan);
public record RecuperarCuentaRequest(string Email, string Codigo, string NuevaPassword);
public record RecuperacionExitosaResponse(int CodigosQueQuedan);
