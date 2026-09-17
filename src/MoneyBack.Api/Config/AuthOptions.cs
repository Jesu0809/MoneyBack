namespace MoneyBack.Api.Config;

public class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>
    /// Código de invitación con el que se siembra la tabla CodigosInvitacion
    /// la primera vez que la app arranca contra una base vacía. Después de
    /// eso, el código vigente vive en la base de datos y el SuperAdmin lo
    /// puede rotar desde /api/admin sin tocar esta configuración.
    /// </summary>
    public string CodigoInvitacionInicial { get; set; } = string.Empty;
}
