namespace MoneyBack.Web.Models;

public record UsuarioAdminResponse(int Id, string Nombre, string Email, DateTime FechaCreacion, List<string> Roles);

public record RotarCodigoInvitacionRequest(string NuevoCodigo);

public record CodigoInvitacionInfo(int Id, DateTime CreadoEn);
