namespace MoneyBack.Api.Dtos;

public record RegistrarUsuarioRequest(string Nombre, string Email, string Password, string CodigoInvitacion);

public record LoginRequest(string Email, string Password);

public record RefrescarTokenRequest(string RefreshToken);

public record AuthResponse(string AccessToken, string RefreshToken, DateTime AccessTokenExpiraEn);

public record PerfilResponse(int Id, string Nombre, string Email, IReadOnlyList<string> Roles);

public record ActualizarPerfilRequest(string Nombre);

public record CambiarPasswordRequest(string PasswordActual, string PasswordNueva);

public record RotarCodigoInvitacionRequest(string NuevoCodigo);

public record UsuarioAdminResponse(int Id, string Nombre, string Email, DateTime FechaCreacion, IReadOnlyList<string> Roles);
