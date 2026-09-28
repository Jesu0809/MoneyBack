using MoneyBack.Api.Models.DiaADia;

namespace MoneyBack.Api.Tests;

/// <summary>
/// No es una verificación: escribe un PDF de muestra a disco para poder
/// mirarlo con los ojos. Se corre a mano con
/// `dotnet test --filter GuardarMuestra` y la ruta se pasa por MUESTRA_PDF.
/// </summary>
public class MuestraPdf : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public MuestraPdf(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task GuardarMuestra()
    {
        var destino = Environment.GetEnvironmentVariable("MUESTRA_PDF");
        if (string.IsNullOrWhiteSpace(destino)) return;

        var (cliente, _) = await _factory.CrearClienteAutenticadoAsync();

        var categorias = new[]
        {
            ("Mercado", TipoCategoria.Gasto), ("Transporte", TipoCategoria.Gasto),
            ("Restaurantes", TipoCategoria.Gasto), ("Servicios", TipoCategoria.Gasto),
            ("Salud", TipoCategoria.Gasto), ("Arriendo", TipoCategoria.Gasto),
            ("Sueldo", TipoCategoria.Ingreso)
        };

        var creadas = new Dictionary<string, int>();
        foreach (var (nombre, tipo) in categorias)
        {
            creadas[nombre] = (await cliente.CrearCategoriaAsync(nombre, tipo)).Id;
        }

        var azar = new Random(7);
        await cliente.RegistrarMovimientoAsync(creadas["Sueldo"], 1_915_000m, nota: "Quincena");
        await cliente.RegistrarMovimientoAsync(creadas["Arriendo"], 1_100_000m, nota: "Pago mensual");

        string[] sitios =
        [
            "EXITO CHAPINERO", "D1 CALLE 63", "ARA SUBA", "TRANSMILENIO",
            "UBER TRIP", "DROGUERIA CAFAM", "EPM ENERGIA", "CREPES & WAFFLES",
            "OLIMPICA", "RAPPI", "CINE COLOMBIA", "TERPEL"
        ];

        foreach (var _ in Enumerable.Range(0, 28))
        {
            var nombre = categorias[azar.Next(0, 5)].Item1;
            await cliente.RegistrarMovimientoAsync(
                creadas[nombre], azar.Next(8, 320) * 1000m,
                fecha: DateTime.UtcNow.AddDays(-azar.Next(0, 45)),
                nota: sitios[azar.Next(sitios.Length)]);
        }

        var respuesta = await cliente.GetAsync("/api/reportes/exportar/pdf");
        respuesta.EnsureSuccessStatusCode();
        await File.WriteAllBytesAsync(destino, await respuesta.Content.ReadAsByteArrayAsync());
    }
}
