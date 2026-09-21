namespace MoneyBack.Web.Models;

public record RegistrarUsuarioRequest(string Nombre, string Email, string Password, string CodigoInvitacion);

public record LoginRequest(string Email, string Password);

public record RefrescarTokenRequest(string RefreshToken);

public record AuthResponse(string AccessToken, string RefreshToken, DateTime AccessTokenExpiraEn);

public record PerfilResponse(int Id, string Nombre, string Email, List<string> Roles, int? DiaPago1, int? DiaPago2);

public record ActualizarPerfilRequest(string Nombre);

public record ActualizarDiasPagoRequest(int? DiaPago1, int? DiaPago2);

public record CambiarPasswordRequest(string PasswordActual, string PasswordNueva);
