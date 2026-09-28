using System.Net;
using System.Text;
using MoneyBack.Api.Models.DiaADia;

namespace MoneyBack.Api.Tests;

/// <summary>
/// Reportes no tenía ni una prueba. Es la parte de la app que sale del
/// teléfono: un PDF se le manda al banco, a la caja de compensación o se
/// imprime — si sale mal, sale mal frente a alguien más.
/// </summary>
public class ReportesTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ReportesTests(ApiFactory factory) => _factory = factory;

    private async Task<HttpClient> ConMovimientosAsync()
    {
        var (cliente, _) = await _factory.CrearClienteAutenticadoAsync();
        var mercado = await cliente.CrearCategoriaAsync("Mercado", TipoCategoria.Gasto);
        var transporte = await cliente.CrearCategoriaAsync("Transporte", TipoCategoria.Gasto);
        var sueldo = await cliente.CrearCategoriaAsync("Sueldo", TipoCategoria.Ingreso);

        await cliente.RegistrarMovimientoAsync(sueldo.Id, 1_915_000m);
        await cliente.RegistrarMovimientoAsync(mercado.Id, 320_500m);
        await cliente.RegistrarMovimientoAsync(mercado.Id, 87_900m);
        await cliente.RegistrarMovimientoAsync(transporte.Id, 45_000m);

        return cliente;
    }

    [Fact]
    public async Task ElPdfSeGeneraYEsUnPdfDeVerdad()
    {
        var cliente = await ConMovimientosAsync();

        var respuesta = await cliente.GetAsync("/api/reportes/exportar/pdf");
        respuesta.EnsureSuccessStatusCode();

        Assert.Equal("application/pdf", respuesta.Content.Headers.ContentType?.MediaType);
        var bytes = await respuesta.Content.ReadAsByteArrayAsync();

        // Firma de archivo PDF. Sin esto, un archivo de error de 200 bytes
        // pasaría la prueba igual.
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.True(bytes.Length > 3000, $"El PDF pesa {bytes.Length} bytes: se generó vacío.");
    }

    /// <summary>
    /// Sin movimientos el PDF tiene que decirlo. Una tabla vacía se lee como
    /// un archivo corrupto, y el usuario cree que la descarga falló.
    /// </summary>
    [Fact]
    public async Task SinMovimientos_ElPdfIgualSeGeneraYNoQuedaVacio()
    {
        var (cliente, _) = await _factory.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.GetAsync("/api/reportes/exportar/pdf");
        respuesta.EnsureSuccessStatusCode();

        var bytes = await respuesta.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.True(bytes.Length > 1000);
    }

    /// <summary>
    /// El servidor corre en UTC y con cultura invariante: un formato "MMM"
    /// escribía "Dec" y "Aug" en un reporte que solo se lee en español.
    /// </summary>
    [Fact]
    public async Task ElPdfNoTraeMesesEnIngles()
    {
        var cliente = await ConMovimientosAsync();

        var respuesta = await cliente.GetAsync(
            "/api/reportes/exportar/pdf?desde=2026-08-01&hasta=2026-12-31");
        var bytes = await respuesta.Content.ReadAsByteArrayAsync();
        var texto = Encoding.Latin1.GetString(bytes);

        foreach (var mes in new[] { "August", "December", "Aug ", "Dec " })
        {
            Assert.DoesNotContain(mes, texto);
        }
    }

    [Fact]
    public async Task ElExcelSeGeneraYEsUnXlsx()
    {
        var cliente = await ConMovimientosAsync();

        var respuesta = await cliente.GetAsync("/api/reportes/exportar/excel");
        respuesta.EnsureSuccessStatusCode();

        var bytes = await respuesta.Content.ReadAsByteArrayAsync();
        // Un .xlsx es un zip: empieza con "PK".
        Assert.Equal("PK", Encoding.ASCII.GetString(bytes, 0, 2));
    }

    /// <summary>
    /// Un reporte que incluya movimientos de otra persona sería una fuga de
    /// datos que además se exporta a un archivo y se comparte.
    /// </summary>
    [Fact]
    public async Task ElReporteSoloTraeLosMovimientosDeQuienLoPide()
    {
        var mio = await ConMovimientosAsync();

        var (ajeno, _) = await _factory.CrearClienteAutenticadoAsync();
        var categoriaAjena = await ajeno.CrearCategoriaAsync("SecretoDelOtro", TipoCategoria.Gasto);
        await ajeno.RegistrarMovimientoAsync(categoriaAjena.Id, 999_999m);

        var respuesta = await mio.GetAsync("/api/reportes/exportar/pdf");
        var texto = Encoding.Latin1.GetString(await respuesta.Content.ReadAsByteArrayAsync());

        Assert.DoesNotContain("SecretoDelOtro", texto);
    }

    [Fact]
    public async Task SinSesion_NoSePuedeDescargarNada()
    {
        var anonimo = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonimo.GetAsync("/api/reportes/exportar/pdf")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonimo.GetAsync("/api/reportes/exportar/excel")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonimo.GetAsync("/api/reportes/anual?anio=2026")).StatusCode);
    }

    [Fact]
    public async Task ElResumenAnualTraeLosDoceMeses()
    {
        var cliente = await ConMovimientosAsync();

        var resumen = await cliente.GetFromJsonAsync<MoneyBack.Api.Dtos.ResumenAnualResponse>(
            $"/api/reportes/anual?anio={DateTime.UtcNow.Year}");

        Assert.NotNull(resumen);
        Assert.Equal(12, resumen!.PorMes.Count);
        Assert.Equal(1_915_000m, resumen.TotalIngresos);
        Assert.Equal(453_400m, resumen.TotalGastos);
    }
}
