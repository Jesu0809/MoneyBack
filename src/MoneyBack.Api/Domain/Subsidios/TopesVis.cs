using MoneyBack.Api.Models.Subsidios;

namespace MoneyBack.Api.Domain.Subsidios;

/// <summary>
/// Múltiplos de SMMLV para cada tope VIS/VIP (Decreto 1467/2019 y normativa
/// vigente). Son constantes porque no cambian con el SMMLV; lo que cambia
/// cada enero es el valor en pesos, que se calcula contra SmmlvVigente.
/// </summary>
public static class TopesVis
{
    private static readonly Dictionary<TipoTopeVis, decimal> MultiplosSmmlv = new()
    {
        [TipoTopeVis.General] = 135m,
        [TipoTopeVis.Decreto1467] = 150m,
        [TipoTopeVis.RenovacionUrbana] = 175m,
        [TipoTopeVis.Vip] = 90m,
    };

    public static decimal MultiploSmmlv(TipoTopeVis tipo) => MultiplosSmmlv[tipo];

    public static decimal TopeEnPesos(TipoTopeVis tipo, decimal smmlvVigente) =>
        MultiploSmmlv(tipo) * smmlvVigente;
}
