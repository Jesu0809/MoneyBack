using MoneyBack.Api.Models.DiaADia;
using MoneyBack.Api.Services;

namespace MoneyBack.Api.Tests;

/// <summary>
/// Este intérprete es el cerebro de los dos atajos (el que escribes tú en el
/// Botón de Acción y, después, el SMS del banco). Si se equivoca, registra
/// plata mal sin que nadie lo revise — por eso se prueba caso por caso, sobre
/// todo la parte de separadores de miles, que en Colombia es donde más fácil
/// se cuela un error de 1.000x.
/// </summary>
public class InterpretadorTextoTests
{
    private static readonly List<Categoria> Categorias =
    [
        new() { Id = 1, Nombre = "Mercado", Tipo = TipoCategoria.Gasto, Activa = true },
        new() { Id = 2, Nombre = "Transporte", Tipo = TipoCategoria.Gasto, Activa = true },
        new() { Id = 3, Nombre = "Salud", Tipo = TipoCategoria.Gasto, Activa = true },
        new() { Id = 4, Nombre = "Otros gastos", Tipo = TipoCategoria.Gasto, Activa = true },
        new() { Id = 5, Nombre = "Salario", Tipo = TipoCategoria.Ingreso, Activa = true },
    ];

    [Theory]
    [InlineData("15000 mercado", 15000)]
    [InlineData("mercado 15000", 15000)]
    [InlineData("15.000 mercado", 15000)]          // punto = miles en Colombia
    [InlineData("$15.000 mercado", 15000)]
    [InlineData("$ 15.000 en mercado", 15000)]
    [InlineData("1.500.000 mercado", 1500000)]
    [InlineData("15 mil mercado", 15000)]
    [InlineData("15k mercado", 15000)]
    [InlineData("gaste 15000 en mercado", 15000)]
    public void ExtraeElMontoCorrecto(string texto, decimal esperado)
    {
        var resultado = InterpretadorTexto.Interpretar(texto, Categorias);

        Assert.True(resultado.Exito, resultado.Razon);
        Assert.Equal(esperado, resultado.Monto);
    }

    [Theory]
    [InlineData("15000 mercado", "Mercado")]
    [InlineData("15000 MERCADO", "Mercado")]        // sin importar mayúsculas
    [InlineData("15000 merc", "Mercado")]           // prefijo
    [InlineData("20000 transporte", "Transporte")]
    [InlineData("30000 salud", "Salud")]
    [InlineData("30000 salúd", "Salud")]            // con tilde de más
    [InlineData("50000 otros gastos", "Otros gastos")]
    public void EncuentraLaCategoriaCorrecta(string texto, string nombreEsperado)
    {
        var resultado = InterpretadorTexto.Interpretar(texto, Categorias);

        Assert.True(resultado.Exito, resultado.Razon);
        Assert.Equal(nombreEsperado, resultado.Categoria!.Nombre);
    }

    [Fact]
    public void UnIngreso_SeReconocePorSuCategoria_NoPorPalabrasClave()
    {
        var resultado = InterpretadorTexto.Interpretar("3000000 salario", Categorias);

        Assert.True(resultado.Exito);
        Assert.Equal(TipoCategoria.Ingreso, resultado.Categoria!.Tipo);
        Assert.Equal(3_000_000m, resultado.Monto);
    }

    [Fact]
    public void SinMonto_ExplicaQueFalta()
    {
        var resultado = InterpretadorTexto.Interpretar("mercado", Categorias);

        Assert.False(resultado.Exito);
        Assert.Contains("monto", resultado.Razon!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SinCategoriaReconocible_ExplicaYDaUnEjemplo()
    {
        var resultado = InterpretadorTexto.Interpretar("15000 pizza", Categorias);

        Assert.False(resultado.Exito);
        Assert.Contains("categoría", resultado.Razon!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TextoVacio_NoRevienta()
    {
        Assert.False(InterpretadorTexto.Interpretar("", Categorias).Exito);
        Assert.False(InterpretadorTexto.Interpretar("   ", Categorias).Exito);
        Assert.False(InterpretadorTexto.Interpretar(null, Categorias).Exito);
    }

    [Fact]
    public void SinCategorias_LoDiceClaro()
    {
        var resultado = InterpretadorTexto.Interpretar("15000 mercado", []);

        Assert.False(resultado.Exito);
        Assert.Contains("categorías", resultado.Razon!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// El atajo de SMS manda el mensaje del banco tal cual. El banco nombra el
    /// comercio ("EXITO", "RAPPI"), no la categoría, así que la persona elige
    /// una al configurar el atajo y llega por categoriaPorDefecto.
    /// </summary>
    [Theory]
    [InlineData("Bancolombia le informa Compra por $150.000 en EXITO 21/09/2026 18:32. Inquietudes al 018000931987", 150000)]
    [InlineData("Bancolombia: Pago por $45.000 a NEQUI. Saldo disponible $1.234.567", 45000)]
    [InlineData("Davivienda informa transaccion por $23.900 en RAPPI el 22/09/2026", 23900)]
    public void UnSmsDelBanco_SacaElMontoYUsaLaCategoriaPorDefecto(string sms, decimal esperado)
    {
        var resultado = InterpretadorTexto.Interpretar(sms, Categorias, "Otros gastos");

        Assert.True(resultado.Exito, resultado.Razon);
        Assert.Equal(esperado, resultado.Monto);
        Assert.Equal("Otros gastos", resultado.Categoria!.Nombre);
    }

    /// <summary>
    /// El saldo que el banco informa después del movimiento suele ser mucho
    /// más grande: tomar ese número en vez del de la compra registraría un
    /// gasto enorme e invisible. El primer monto del mensaje es el correcto.
    /// </summary>
    [Fact]
    public void ConSaldoEnElMismoSms_TomaElMontoDeLaTransaccion_NoElSaldo()
    {
        var resultado = InterpretadorTexto.Interpretar(
            "Bancolombia: Pago por $45.000 a NEQUI. Saldo disponible $1.234.567", Categorias, "Otros gastos");

        Assert.Equal(45_000m, resultado.Monto);
    }

    /// <summary>
    /// El nombre del comercio puede chocar con el de una categoría por pura
    /// casualidad: "MERCADO LIBRE" no es mercado. Si el destino dependiera de
    /// esa coincidencia, el mismo atajo mandaría gastos a categorías distintas
    /// sin que nadie entienda por qué.
    /// </summary>
    [Theory]
    [InlineData("Bancolombia Compra por $32.000 en MERCADO LIBRE")]
    [InlineData("Compra por $19.000 en SALUD TOTAL EPS")]
    [InlineData("Compra por $60.000 en ROPA Y MODA SAS")]
    public void CategoriaForzada_LeGanaAlNombreDelComercio(string sms)
    {
        var resultado = InterpretadorTexto.Interpretar(sms, Categorias, categoriaForzada: "Otros gastos");

        Assert.True(resultado.Exito, resultado.Razon);
        Assert.Equal("Otros gastos", resultado.Categoria!.Nombre);
    }

    [Fact]
    public void CategoriaForzadaInexistente_LoDiceClaro()
    {
        var resultado = InterpretadorTexto.Interpretar("compra por $10.000", Categorias, categoriaForzada: "Inventada");

        Assert.False(resultado.Exito);
        Assert.Contains("Inventada", resultado.Razon!);
    }

    [Fact]
    public void SiElTextoSiNombraCategoria_EsaGanaSobreLaPorDefecto()
    {
        var resultado = InterpretadorTexto.Interpretar("15000 transporte", Categorias, "Otros gastos");

        Assert.Equal("Transporte", resultado.Categoria!.Nombre);
    }

    [Fact]
    public void UnaCategoriaPorDefectoQueNoExiste_LoDiceClaro()
    {
        var resultado = InterpretadorTexto.Interpretar("compra por $50.000 en EXITO", Categorias, "Inventada");

        Assert.False(resultado.Exito);
        Assert.Contains("Inventada", resultado.Razon!);
    }

    /// <summary>
    /// Una palabra corta no debe disparar una categoría por accidente: si
    /// "ro" bastara para encontrar "Ropa", cualquier texto suelto registraría
    /// plata en la categoría equivocada.
    /// </summary>
    [Fact]
    public void UnPrefijoMuyCorto_NoActivaUnaCategoria()
    {
        var conRopa = new List<Categoria> { new() { Id = 9, Nombre = "Ropa", Tipo = TipoCategoria.Gasto, Activa = true } };

        var resultado = InterpretadorTexto.Interpretar("15000 ro", conRopa);

        Assert.False(resultado.Exito);
    }
}
