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
    /// SMS reales de Davivienda. Usa coma como separador de miles ("33,800"
    /// son treinta y tres mil ochocientos) y mete números en el nombre del
    /// comercio ("OXXO CALLE 100", "PRESTO ISERRA 100").
    ///
    /// El último caso es el que importa: un monto de menos de mil no lleva
    /// separador, así que "agarrar el primer número" sacaba el 100 de "CALLE
    /// 100" en vez del monto real. Una compra de $950 quedaba como $100.
    /// </summary>
    [Theory]
    [InlineData("DAVIbank: Realizaste  transaccion en PRESTO ISERRA 100 por 33,800 con tu tarjeta Clasica 2026/09/16 14:21:49.", 33800)]
    [InlineData("DAVIbank: Enviaste 560,000 a la llave 3186014188 de manera exitosa el 15-09-2026 a las 17:52:41.", 560000)]
    [InlineData("DAVIbank: Realizaste  transaccion en PROMO PARAMO por 2,000 con tu tarjeta Clasica 2026/09/13 21:26:35.", 2000)]
    [InlineData("DAVIbank: Realizaste  transaccion en PWSCCO*BBC PUB ANDINO por 71,500 con tu tarjeta Clasica 2026/09/06 18:57:17.", 71500)]
    [InlineData("DAVIbank: Realizaste  transaccion en OXXO CALLE 100 por 13,500 con tu tarjeta Clasica 2026/09/02 8:08:56.", 13500)]
    [InlineData("DAVIbank: Realizaste  transaccion en OXXO CALLE 100 por 950 con tu tarjeta Clasica 2026/09/02 8:08:56.", 950)]
    [InlineData("DAVIbank: Realizaste  transaccion en PRESTO ISERRA 100 por 480 con tu tarjeta Clasica 2026/09/16 14:21:49.", 480)]
    public void SmsRealesDeDavivienda(string sms, decimal esperado)
    {
        Assert.Equal(esperado, InterpretadorTexto.ExtraerMonto(sms));
    }

    /// <summary>
    /// No se puede escribir una regla para un formato que nadie ha visto, así
    /// que la garantía no es "siempre acierta" sino "nunca inventa". Si el
    /// número no viene marcado como plata y el texto trae varios números
    /// (fechas, teléfonos, direcciones), se niega y manda a registrar a mano.
    /// Un gasto sin registrar se nota; uno con el monto equivocado aparece
    /// semanas después cuadrando cuentas.
    /// </summary>
    [Theory]
    [InlineData("Nu: compra 45000 en TIENDA 24 el 23-09-2026")]
    [InlineData("Banco X: 12000 TIENDA 5 ref 889")]
    [InlineData("Movimiento 7800 en LOCAL 42 terminal 9")]
    public void FormatoDesconocidoYAmbiguo_SeNiegaEnVezDeAdivinar(string sms)
    {
        var resultado = InterpretadorTexto.Interpretar(sms, Categorias, categoriaForzada: "Otros gastos");

        Assert.False(resultado.Exito);
        Assert.Contains("a mano", resultado.Razon!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Pero un banco desconocido que sí marque el monto tiene que funcionar
    /// sin que nadie le escriba una regla: basta con que use "$" o "por".
    /// </summary>
    [Theory]
    [InlineData("Nu: Compra aprobada por $45.000 en TIENDA 24 el 23-09-2026", 45000)]
    [InlineData("Banco Y: Pagaste $12.500 en LOCAL 5 ref 889", 12500)]
    [InlineData("Cualquier banco: transaccion por 7,800 en SITIO 42", 7800)]
    public void FormatoDesconocidoPeroConSenal_Funciona(string sms, decimal esperado)
    {
        var resultado = InterpretadorTexto.Interpretar(sms, Categorias, categoriaForzada: "Otros gastos");

        Assert.True(resultado.Exito, resultado.Razon);
        Assert.Equal(esperado, resultado.Monto);
    }

    /// <summary>
    /// Lo que la persona escribe en el Botón de Acción no debe verse afectado:
    /// "15000 mercado" trae un solo número, no hay nada que confundir.
    /// </summary>
    [Theory]
    [InlineData("15000 mercado", 15000)]
    [InlineData("mercado 15000", 15000)]
    public void TextoEscritoAMano_SigueFuncionandoSinSenales(string texto, decimal esperado)
    {
        var resultado = InterpretadorTexto.Interpretar(texto, Categorias);

        Assert.True(resultado.Exito, resultado.Razon);
        Assert.Equal(esperado, resultado.Monto);
    }

    /// <summary>
    /// Nu solo avisa por notificación de su app — no manda SMS ni correo — así
    /// que la única vía es leer el texto de una captura. El formato es distinto
    /// al de Davivienda: "$ 6.600", con espacio y punto de miles.
    /// </summary>
    [Theory]
    [InlineData("Nu\nHace 2 h\nTostao Coffee and Bread. Bogotá, Bogotá\n$ 6.600", 6600)]
    [InlineData("Nu\nDollarcity Nomad Salitre. Bogotá, Bogotá\n$ 22.500", 22500)]
    [InlineData("Nu\nZelo Group. Bogotá, Bogotá\n$ 3.400", 3400)]
    [InlineData("Nu\nTostao Coffee and Bread. Bogotá, Bogotá\n$ 900", 900)]
    public void NotificacionDeNu_LeidaDeUnaCaptura(string ocr, decimal esperado)
    {
        var resultado = InterpretadorTexto.Interpretar(ocr, Categorias, categoriaForzada: "Otros gastos", vieneDeCaptura: true);

        Assert.True(resultado.Exito, resultado.Razon);
        Assert.Equal(esperado, resultado.Monto);
    }

    /// <summary>
    /// Una captura del centro de notificaciones trae varias transacciones
    /// apiladas. Quedarse con la primera registraría la compra equivocada sin
    /// que nadie se entere — mejor pedir que recorte.
    /// </summary>
    [Fact]
    public void CapturaConVariasNotificaciones_PideQueLaRecorte()
    {
        var ocr = "Nu Hace 2 h\nTostao Coffee and Bread. Bogotá, Bogotá\n$ 6.600\n" +
                  "Nu ayer, 6:50 p.m.\nDollarcity Nomad Salitre. Bogotá, Bogotá\n$ 22.500\n" +
                  "Nu ayer, 12:16 p.m.\nZelo Group. Bogotá, Bogotá\n$ 3.400";

        var resultado = InterpretadorTexto.Interpretar(ocr, Categorias, categoriaForzada: "Otros gastos", vieneDeCaptura: true);

        Assert.False(resultado.Exito);
        Assert.Contains("recórtala", resultado.Razon!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// La misma regla NO debe aplicar a los SMS: ahí un segundo monto suele ser
    /// el saldo informado después del movimiento, y el primero sigue siendo el
    /// correcto. Sin esta distinción, arreglar lo de Nu rompería Bancolombia.
    /// </summary>
    [Fact]
    public void UnSmsConSaldoAdicional_SigueRegistrandoElPrimerMonto()
    {
        var sms = "Bancolombia: Pago por $45.000 a NEQUI. Saldo disponible $1.234.567";

        var resultado = InterpretadorTexto.Interpretar(sms, Categorias, categoriaForzada: "Otros gastos");

        Assert.True(resultado.Exito, resultado.Razon);
        Assert.Equal(45_000m, resultado.Monto);
    }

    /// <summary>
    /// Sin el nombre del comercio, la lista del día a día muestra
    /// "Otros gastos / Otros gastos" y toca abrir la app del banco para
    /// recordar en qué se gastó. Son dos formatos distintos, uno por banco.
    /// </summary>
    [Theory]
    [InlineData("DAVIbank: Realizaste  transaccion en PRESTO ISERRA 100 por 33,800 con tu tarjeta Clasica 2026/09/16 14:21:49.", "PRESTO ISERRA 100")]
    [InlineData("DAVIbank: Realizaste  transaccion en OXXO CALLE 100 por 13,500 con tu tarjeta Clasica", "OXXO CALLE 100")]
    [InlineData("DAVIbank: Realizaste  transaccion en PWSCCO*BBC PUB ANDINO por 71,500 con tu tarjeta Clasica", "PWSCCO*BBC PUB ANDINO")]
    [InlineData("Nu\nTostao Coffee and Bread. Bogotá, Bogotá\n$ 6.600", "Tostao Coffee and Bread")]
    [InlineData("Dollarcity Nomad Salitre. Bogotá, Bogotá\n$ 22.500", "Dollarcity Nomad Salitre")]
    public void SacaElNombreDelComercio(string texto, string esperado)
    {
        Assert.Equal(esperado, InterpretadorTexto.ExtraerComercio(texto));
    }

    /// <summary>
    /// Escribir "15000 mercado" a mano no trae comercio. Mejor sin nota que
    /// con un pedazo de frase que no significa nada.
    /// </summary>
    [Theory]
    [InlineData("15000 mercado")]
    [InlineData("")]
    [InlineData("   ")]
    public void SinComercioReconocible_NoInventaNota(string texto)
    {
        Assert.Null(InterpretadorTexto.ExtraerComercio(texto));
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
