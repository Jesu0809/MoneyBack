namespace MoneyBack.Api.Config;

/// <summary>
/// El SMMLV cambia cada enero. Se configura en appsettings (no en código)
/// para poder actualizarlo sin recompilar; todos los topes y subsidios se
/// calculan como múltiplos de este valor.
/// </summary>
public class SubsidiosOptions
{
    public const string SectionName = "Subsidios";

    public decimal SmmlvVigente { get; set; }
}
