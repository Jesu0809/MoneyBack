namespace MoneyBack.Web.Models;

public record CrearTokenAtajoRequest(string? Nombre);

public record TokenAtajoCreadoResponse(int Id, string Token);

public record TokenAtajoResponse(int Id, string? Nombre, DateTime FechaCreacion, DateTime? UltimoUso);
