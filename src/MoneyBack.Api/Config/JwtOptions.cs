namespace MoneyBack.Api.Config;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;

    /// <summary>Clave secreta para firmar tokens (HMAC-SHA256, mínimo 32 bytes). Va en user-secrets / env var, nunca en el repo.</summary>
    public string ClaveSecreta { get; set; } = string.Empty;

    public int AccessTokenMinutos { get; set; } = 15;
    public int RefreshTokenDias { get; set; } = 30;
}
