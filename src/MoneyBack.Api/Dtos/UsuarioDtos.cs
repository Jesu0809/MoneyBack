namespace MoneyBack.Api.Dtos;

public record CrearUsuarioRequest(string Nombre, string Email);

public record UsuarioResponse(int Id, string Nombre, string Email);
