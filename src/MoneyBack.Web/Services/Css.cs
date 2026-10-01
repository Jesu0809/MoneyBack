using System.Globalization;

namespace MoneyBack.Web.Services;

/// <summary>
/// Números para meter dentro de CSS.
///
/// CSS solo entiende el punto como separador decimal. El teléfono de quien
/// usa esto está en español, donde el separador es la coma, y cualquier
/// número interpolado directo en un atributo style sale con coma: "163,8deg",
/// "45,5%". Eso no es un valor feo, es un valor INVÁLIDO — el navegador
/// descarta la declaración entera y lo que se dibujaba desaparece.
///
/// Pasó tres veces y las tres de forma invisible desde un escritorio en
/// inglés: la torta de gastos por categoría no se pintaba, las barras del
/// reporte anual quedaban planas y la barra de progreso de las metas nunca
/// se llenaba. Con una sola categoría o con un porcentaje redondo sí
/// funcionaba, que es lo que lo hacía tan difícil de ver.
/// </summary>
public static class Css
{
    /// <summary>Con punto decimal siempre, pase lo que pase con el idioma.</summary>
    public static string Numero(decimal valor) => valor.ToString("0.##", CultureInfo.InvariantCulture);

    public static string Numero(double valor) => valor.ToString("0.##", CultureInfo.InvariantCulture);
}
