using System.Net.Http.Json;
using MoneyBack.Api.Dtos;
using MoneyBack.Api.Models.DiaADia;

namespace MoneyBack.Api.Tests;

public class PresupuestosTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public PresupuestosTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task MontoGastado_SoloSumaElMesYAnioDelPresupuesto_NoOtrosMeses()
    {
        var (cliente, _) = await _factory.CrearClienteAutenticadoAsync();
        var categoria = await cliente.CrearCategoriaAsync("Mercado", TipoCategoria.Gasto);

        var guardar = await cliente.PostAsJsonAsync("/api/presupuestos", new GuardarPresupuestoRequest(categoria.Id, 500_000m, 3, 2026));
        guardar.EnsureSuccessStatusCode();

        // Las horas importan: el mes se corta en la medianoche de Bogotá, no
        // en la de UTC. "1 de abril 00:00 UTC" son todavía las 7 p.m. del 31
        // de marzo acá, así que para probar "fuera del mes" hay que usar una
        // hora que sea inequívocamente del otro mes en los dos husos.
        await cliente.RegistrarMovimientoAsync(categoria.Id, 100_000m, new DateTime(2026, 3, 5, 12, 0, 0));
        await cliente.RegistrarMovimientoAsync(categoria.Id, 120_000m, new DateTime(2026, 3, 20, 12, 0, 0));
        // Fuera del mes: no debe contar.
        await cliente.RegistrarMovimientoAsync(categoria.Id, 999_000m, new DateTime(2026, 4, 1, 12, 0, 0));
        await cliente.RegistrarMovimientoAsync(categoria.Id, 999_000m, new DateTime(2026, 2, 27, 12, 0, 0));

        var presupuestos = await cliente.GetFromJsonAsync<List<PresupuestoResponse>>("/api/presupuestos?mes=3&anio=2026");

        var presupuesto = Assert.Single(presupuestos!);
        Assert.Equal(220_000m, presupuesto.MontoGastado);
        Assert.Equal(500_000m, presupuesto.MontoLimite);
    }

    [Fact]
    public async Task GuardarPresupuestoDosVeces_MismoMesYCategoria_ReemplazaEnVezDeDuplicar()
    {
        var (cliente, _) = await _factory.CrearClienteAutenticadoAsync();
        var categoria = await cliente.CrearCategoriaAsync("Transporte", TipoCategoria.Gasto);

        await cliente.PostAsJsonAsync("/api/presupuestos", new GuardarPresupuestoRequest(categoria.Id, 200_000m, 6, 2026));
        await cliente.PostAsJsonAsync("/api/presupuestos", new GuardarPresupuestoRequest(categoria.Id, 350_000m, 6, 2026));

        var presupuestos = await cliente.GetFromJsonAsync<List<PresupuestoResponse>>("/api/presupuestos?mes=6&anio=2026");

        var presupuesto = Assert.Single(presupuestos!);
        Assert.Equal(350_000m, presupuesto.MontoLimite);
    }

    /// <summary>
    /// Un gasto del último día del mes, de noche en Bogotá, tiene que
    /// contar contra ESE mes.
    ///
    /// La pantalla de topes cortaba el mes en medianoche UTC y
    /// AlertasPresupuestoService en medianoche de Bogotá: el aviso push
    /// decía un número y la pantalla otro, y los gastos de la noche del
    /// último día se iban al mes siguiente.
    /// </summary>
    [Fact]
    public async Task ElGastoDeLaNocheDelUltimoDiaCuentaContraElTopeDeEseMes()
    {
        var (cliente, _) = await _factory.CrearClienteAutenticadoAsync();
        var categoria = await cliente.CrearCategoriaAsync("Mercado", TipoCategoria.Gasto);

        await cliente.PostAsJsonAsync("/api/presupuestos",
            new GuardarPresupuestoRequest(categoria.Id, 500_000m, 9, 2026));

        // 30 de septiembre, 8:15 p.m. en Bogotá = 1 de octubre 01:15 UTC.
        var esaNoche = new DateTimeOffset(2026, 9, 30, 20, 15, 0, TimeSpan.FromHours(-5)).UtcDateTime;
        await cliente.RegistrarMovimientoAsync(categoria.Id, 120_000m, fecha: esaNoche);

        var deSeptiembre = await cliente.GetFromJsonAsync<List<PresupuestoResponse>>(
            "/api/presupuestos?mes=9&anio=2026");

        Assert.Equal(120_000m, deSeptiembre!.Single(p => p.CategoriaId == categoria.Id).MontoGastado);
    }

    [Fact]
    public async Task YNoSeCuelaEnElTopeDelMesSiguiente()
    {
        var (cliente, _) = await _factory.CrearClienteAutenticadoAsync();
        var categoria = await cliente.CrearCategoriaAsync("Mercado", TipoCategoria.Gasto);

        await cliente.PostAsJsonAsync("/api/presupuestos",
            new GuardarPresupuestoRequest(categoria.Id, 500_000m, 10, 2026));

        var esaNoche = new DateTimeOffset(2026, 9, 30, 20, 15, 0, TimeSpan.FromHours(-5)).UtcDateTime;
        await cliente.RegistrarMovimientoAsync(categoria.Id, 120_000m, fecha: esaNoche);

        var deOctubre = await cliente.GetFromJsonAsync<List<PresupuestoResponse>>(
            "/api/presupuestos?mes=10&anio=2026");

        Assert.Equal(0m, deOctubre!.Single(p => p.CategoriaId == categoria.Id).MontoGastado);
    }

    /// <summary>
    /// Y el primer gasto del mes, de madrugada, tampoco puede caerse al mes
    /// anterior: a la 1 a.m. de Bogotá en UTC siguen siendo las 6 a.m. del
    /// mismo día, pero el corte tiene que ser el de acá igual.
    /// </summary>
    [Fact]
    public async Task ElGastoDeLaMadrugadaDelPrimeroCuentaEnSuMes()
    {
        var (cliente, _) = await _factory.CrearClienteAutenticadoAsync();
        var categoria = await cliente.CrearCategoriaAsync("Mercado", TipoCategoria.Gasto);

        await cliente.PostAsJsonAsync("/api/presupuestos",
            new GuardarPresupuestoRequest(categoria.Id, 500_000m, 10, 2026));

        var madrugada = new DateTimeOffset(2026, 10, 1, 1, 0, 0, TimeSpan.FromHours(-5)).UtcDateTime;
        await cliente.RegistrarMovimientoAsync(categoria.Id, 80_000m, fecha: madrugada);

        var deOctubre = await cliente.GetFromJsonAsync<List<PresupuestoResponse>>(
            "/api/presupuestos?mes=10&anio=2026");

        Assert.Equal(80_000m, deOctubre!.Single(p => p.CategoriaId == categoria.Id).MontoGastado);
    }
}
