namespace MoneyBack.Api.Domain.Subsidios;

/// <summary>
/// Las cifras reales del mercado de vivienda colombiano con las que se arma un
/// plan de pago: tasas, montos de subsidio y porcentajes de financiación.
///
/// TODAS llevan fecha y fuente, y la fecha se le muestra a la persona. Esto no
/// es burocracia: son datos que cambian —las tasas cada mes, los subsidios cada
/// enero con el salario mínimo, los cupos cuando se acaban— y una cifra vieja
/// presentada como actual es peor que no dar ninguna, porque alguien puede
/// tomar una decisión de cien millones de pesos creyéndole. Mientras se vea
/// "datos a septiembre de 2026", quien lea sabe cuánto confiar.
///
/// Para actualizar: revisar cada fuente, cambiar el número y mover
/// <see cref="VigenteDesde"/>. No cambiar un número sin mover la fecha.
/// </summary>
public static class ParametrosVivienda
{
    public static readonly DateOnly VigenteDesde = new(2026, 9, 1);

    /// <summary>
    /// Tasas efectivas anuales. Fuente: FNA (fna.gov.co/sobre-el-fna/tasas) y
    /// el rango de bancos que publica la Superintendencia Financiera.
    /// </summary>
    public const decimal TasaFnaVis = 0.1079m;
    public const decimal TasaFnaNoVis = 0.1215m;

    /// <summary>
    /// "Tasa Social" del FNA: 7% E.A. para hogares de hasta 2 SMMLV que
    /// compren VIS o VIP. Tiene cupos limitados, así que se muestra como una
    /// posibilidad a averiguar, nunca como un hecho.
    /// </summary>
    public const decimal TasaFnaSocial = 0.07m;
    public const decimal IngresoMaximoTasaSocialSmmlv = 2m;

    public const decimal TasaBancoMinima = 0.118m;
    public const decimal TasaBancoMaxima = 0.177m;

    /// <summary>
    /// Subsidio de caja de compensación, en SMMLV, por tramo de ingreso del
    /// hogar. Con Mi Casa Ya prácticamente sin cupos, esta es hoy la vía más
    /// realista, y por eso es la que se calcula en firme.
    /// </summary>
    public const decimal SubsidioCajaHasta2Smmlv = 30m;
    public const decimal SubsidioCajaHasta4Smmlv = 20m;

    /// <summary>
    /// Mi Casa Ya sigue existiendo en el papel y los montos son estos, pero en
    /// 2026 quedan pocos cupos remanentes. Se informa con esa advertencia
    /// pegada: prometer treinta millones que no van a llegar cambiaría la
    /// decisión de compra de alguien.
    /// </summary>
    public const decimal MiCasaYaSisbenBajoSmmlv = 30m;
    public const decimal MiCasaYaSisbenAltoSmmlv = 20m;

    /// <summary>
    /// Puntos porcentuales que el Gobierno cubre de la tasa durante los
    /// primeros 84 meses del crédito.
    /// </summary>
    public const decimal CoberturaTasaVis = 0.04m;
    public const decimal CoberturaTasaVip = 0.05m;
    public const int MesesCoberturaTasa = 84;

    /// <summary>
    /// Cuánto del valor financia cada entidad. El FNA llega al 100% en VIS y
    /// VIP para afiliados que compran su primera vivienda —por eso ahí puede
    /// no hacer falta cuota inicial—; los bancos se quedan en 80% para VIS y
    /// 70% para el resto.
    /// </summary>
    public const decimal FinanciacionFnaVis = 1.00m;
    public const decimal FinanciacionBancoVis = 0.80m;
    public const decimal FinanciacionBancoNoVis = 0.70m;

    /// <summary>
    /// Cuánto del ingreso mensual puede irse en la cuota. Es el filtro que de
    /// verdad usan las entidades para aprobar, así que es también el que
    /// decide si la respuesta a "¿me alcanza?" es sí o no.
    /// </summary>
    public const decimal ProporcionMaximaDelIngreso = 0.30m;

    public const int PlazoMesesTipico = 240;
}
