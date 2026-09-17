namespace MoneyBack.Api.Models.Subsidios;

/// <summary>
/// Categoría de tope VIS/VIP según ubicación del proyecto (Decreto 1467/2019
/// y normativa vigente). El multiplo de SMMLV es fijo; el valor en pesos se
/// recalcula cada enero contra el SMMLV vigente en vez de hardcodearse.
/// </summary>
public enum TipoTopeVis
{
    /// <summary>General: la mayoría de municipios del país. 135 SMMLV.</summary>
    General,

    /// <summary>
    /// 45 municipios del Decreto 1467/2019: Bogotá D.C. y su aglomeración
    /// (Chía, Cajicá, Soacha, Cota, Funza, Facatativá, Madrid, Mosquera,
    /// Zipaquirá, Tabio, Tocancipá, La Calera, Sibaté), además de Cali,
    /// Medellín, Barranquilla, Bucaramanga y sus aglomeraciones. 150 SMMLV.
    /// </summary>
    Decreto1467,

    /// <summary>VIS de renovación urbana, solo proyectos con plan POT específico. 175 SMMLV.</summary>
    RenovacionUrbana,

    /// <summary>Vivienda de Interés Prioritario. 90 SMMLV.</summary>
    Vip
}
