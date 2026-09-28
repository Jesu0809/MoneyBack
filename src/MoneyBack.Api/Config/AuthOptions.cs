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

    /// <summary>
    /// Salida de emergencia: el correo de una cuenta a la que darle el rol
    /// SuperAdmin al arrancar.
    ///
    /// Existe porque el rol se le da a la primera cuenta que se registra y
    /// no hay forma de recuperarlo si se pierde el acceso a esa cuenta: no
    /// hay a quién pedírselo, porque el único que podía darlo era ella.
    /// Ponerlo como secreto de Fly es tan seguro como lo sea esa cuenta de
    /// Fly, que es la misma que puede desplegar código nuevo — o sea, no
    /// abre ninguna puerta que no estuviera abierta.
    ///
    /// Se deja vacío en operación normal. Al usarlo, quitarlo después.
    /// </summary>
    public string PromoverASuperAdmin { get; set; } = string.Empty;
}
