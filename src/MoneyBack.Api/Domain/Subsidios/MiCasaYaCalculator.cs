namespace MoneyBack.Api.Domain.Subsidios;

public record ResultadoMiCasaYa(
    bool Elegible,
    decimal SubsidioEnSmmlv,
    decimal SubsidioEnPesos,
    bool SoloCoberturaTasaFrech,
    string? Nota);

/// <summary>
/// Reglas de Mi Casa Ya por ingreso combinado del hogar, en múltiplos de
/// SMMLV vigente. El tramo de 4-8 SMMLV solo confirma cobertura de tasa
/// FRECH: el monto de subsidio directo en ese rango no está confirmado
/// con una fuente oficial, así que no se inventa un número.
/// </summary>
public static class MiCasaYaCalculator
{
    private const decimal SubsidioMenorA2SmmlvEnSmmlv = 30m;
    private const decimal SubsidioEntre2Y4SmmlvEnSmmlv = 20m;

    public static ResultadoMiCasaYa Calcular(decimal ingresoCombinadoMensual, decimal smmlvVigente, bool cumpleSisbenIvAD20)
    {
        if (!cumpleSisbenIvAD20)
        {
            return new ResultadoMiCasaYa(false, 0, 0, false,
                "No cumple el requisito de Sisbén IV (grupos A1-D20) para Mi Casa Ya.");
        }

        var ingresoEnSmmlv = ingresoCombinadoMensual / smmlvVigente;

        if (ingresoEnSmmlv < 2m)
        {
            return new ResultadoMiCasaYa(true, SubsidioMenorA2SmmlvEnSmmlv,
                SubsidioMenorA2SmmlvEnSmmlv * smmlvVigente, false, null);
        }

        if (ingresoEnSmmlv <= 4m)
        {
            return new ResultadoMiCasaYa(true, SubsidioEntre2Y4SmmlvEnSmmlv,
                SubsidioEntre2Y4SmmlvEnSmmlv * smmlvVigente, false, null);
        }

        if (ingresoEnSmmlv <= 8m)
        {
            return new ResultadoMiCasaYa(true, 0, 0, true,
                "Entre 4 y 8 SMMLV: elegible para cobertura de tasa FRECH, pero el monto " +
                "de subsidio directo en este tramo no está confirmado con una fuente oficial.");
        }

        return new ResultadoMiCasaYa(false, 0, 0, false,
            "El ingreso combinado supera 8 SMMLV: fuera del rango de Mi Casa Ya.");
    }
}
