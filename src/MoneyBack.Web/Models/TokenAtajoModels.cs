namespace MoneyBack.Web.Models;

public record CrearTokenAtajoRequest(string? Nombre);

public record TokenAtajoCreadoResponse(int Id, string Token);

public record TokenAtajoResponse(int Id, string? Nombre, DateTime FechaCreacion, DateTime? UltimoUso);

public record LlamadaAtajoResponse(DateTime Fecha, string? Texto, bool Exito, string? Detalle);
