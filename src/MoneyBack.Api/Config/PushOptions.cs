namespace MoneyBack.Api.Config;

public class PushOptions
{
    public const string SectionName = "Push";

    /// <summary>Clave pública VAPID, se comparte con el frontend (no es secreta).</summary>
    public string VapidPublicKey { get; set; } = string.Empty;

    /// <summary>Clave privada VAPID para firmar los envíos. Va en user-secrets / fly secrets, nunca en el repo.</summary>
    public string VapidPrivateKey { get; set; } = string.Empty;

    /// <summary>Contacto exigido por el protocolo Web Push, formato "mailto:correo@dominio.com".</summary>
    public string VapidSubject { get; set; } = string.Empty;
}
