namespace MoneyBack.Api.Models.Auth;

/// <summary>
/// Código compartido que se debe ingresar para poder registrarse. Solo el
/// más reciente con Activo=true es válido; rotar el código desactiva el
/// anterior en vez de sobrescribirlo, para tener historial de rotaciones.
/// </summary>
public class CodigoInvitacion
{
    public int Id { get; set; }

    public string CodigoHash { get; set; } = string.Empty;

    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

    public int? CreadoPorUsuarioId { get; set; }

    public bool Activo { get; set; } = true;
}
