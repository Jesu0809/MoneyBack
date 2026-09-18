using MoneyBack.Api.Models.DiaADia;

namespace MoneyBack.Api.Dtos;

public record CrearTokenAtajoRequest(string? Nombre);

/// <summary>El token en claro solo viaja en esta respuesta, una sola vez — no se puede volver a consultar.</summary>
public record TokenAtajoCreadoResponse(int Id, string Token);

public record TokenAtajoResponse(int Id, string? Nombre, DateTime FechaCreacion, DateTime? UltimoUso);

public record CategoriaAtajoResponse(int Id, string Nombre, string Icono, TipoCategoria Tipo);

public record CrearMovimientoAtajoRequest(int CategoriaId, decimal Monto, string? Nota);
