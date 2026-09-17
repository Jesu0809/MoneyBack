namespace MoneyBack.Api.Models.Auth;

public static class Roles
{
    /// <summary>
    /// Permisos administrativos (gestionar código de invitación, revocar
    /// sesiones, ver estructura básica de cuentas). Nunca debe usarse para
    /// dar acceso a MetaAhorro/MovimientoMeta de un hogar ajeno — esos
    /// endpoints solo se autorizan por pertenencia al hogar, sin excepción de rol.
    /// </summary>
    public const string SuperAdmin = "SuperAdmin";
}
