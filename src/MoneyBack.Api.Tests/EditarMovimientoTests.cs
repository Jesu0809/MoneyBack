using System.Net;
using System.Net.Http.Json;
using MoneyBack.Api.Dtos;
using MoneyBack.Api.Models.DiaADia;

namespace MoneyBack.Api.Tests;

/// <summary>
/// Antes solo se podía crear y borrar: equivocarse de categoría obligaba a
/// eliminar el movimiento y volverlo a cargar. Esto importa más ahora que el
/// atajo de SMS deja todo en "Otros gastos" y toca reclasificar después.
/// </summary>
public class EditarMovimientoTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public EditarMovimientoTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task CambiarDeCategoria_MueveElGastoYDejaElSaldoIgual()
    {
        var (cliente, _) = await _factory.CrearClienteAutenticadoAsync();
        var comida = await cliente.CrearCategoriaAsync("Comida", TipoCategoria.Gasto);
        var mercado = await cliente.CrearCategoriaAsync("Mercado", TipoCategoria.Gasto);

        var mov = await cliente.RegistrarMovimientoAsync(comida.Id, 50_000m);

        var respuesta = await cliente.PutAsJsonAsync($"/api/movimientos-diaadia/{mov.Id}",
            new ActualizarMovimientoDiaADiaRequest(mercado.Id, 50_000m, null, null));
        respuesta.EnsureSuccessStatusCode();

        var actualizado = (await respuesta.Content.ReadFromJsonAsync<MovimientoDiaADiaResponse>())!;
        Assert.Equal("Mercado", actualizado.CategoriaNombre);

        // El total no cambia: solo se movió de categoría.
        var resumen = await cliente.GetFromJsonAsync<ResumenDiaADiaResponse>("/api/movimientos-diaadia/resumen");
        Assert.Equal(50_000m, resumen!.TotalGastos);
        Assert.Equal(50_000m, resumen.GastosPorCategoria.Single(g => g.CategoriaNombre == "Mercado").Total);
        Assert.DoesNotContain(resumen.GastosPorCategoria, g => g.CategoriaNombre == "Comida");
    }

    [Fact]
    public async Task CorregirElMonto_SeRefleja_SinDuplicarElMovimiento()
    {
        var (cliente, _) = await _factory.CrearClienteAutenticadoAsync();
        var categoria = await cliente.CrearCategoriaAsync("Transporte", TipoCategoria.Gasto);
        var mov = await cliente.RegistrarMovimientoAsync(categoria.Id, 100m);

        await cliente.PutAsJsonAsync($"/api/movimientos-diaadia/{mov.Id}",
            new ActualizarMovimientoDiaADiaRequest(categoria.Id, 950m, null, null));

        var resumen = await cliente.GetFromJsonAsync<ResumenDiaADiaResponse>("/api/movimientos-diaadia/resumen");
        Assert.Equal(950m, resumen!.TotalGastos);

        var movimientos = await cliente.GetFromJsonAsync<List<MovimientoDiaADiaResponse>>("/api/movimientos-diaadia");
        Assert.Single(movimientos!);
    }

    /// <summary>
    /// Caso real del atajo de SMS: entra como gasto pero era un ingreso.
    /// Al cambiar a una categoría de tipo Ingreso, el saldo debe voltear.
    /// </summary>
    [Fact]
    public async Task CambiarAUnaCategoriaDeIngreso_VolteaElSigno()
    {
        var (cliente, _) = await _factory.CrearClienteAutenticadoAsync();
        var gasto = await cliente.CrearCategoriaAsync("Otros gastos", TipoCategoria.Gasto);
        var ingreso = await cliente.CrearCategoriaAsync("Otros ingresos", TipoCategoria.Ingreso);

        var mov = await cliente.RegistrarMovimientoAsync(gasto.Id, 80_000m);
        Assert.Equal(-80_000m, (await cliente.GetFromJsonAsync<ResumenDiaADiaResponse>("/api/movimientos-diaadia/resumen"))!.Balance);

        await cliente.PutAsJsonAsync($"/api/movimientos-diaadia/{mov.Id}",
            new ActualizarMovimientoDiaADiaRequest(ingreso.Id, 80_000m, null, null));

        var resumen = await cliente.GetFromJsonAsync<ResumenDiaADiaResponse>("/api/movimientos-diaadia/resumen");
        Assert.Equal(80_000m, resumen!.Balance);
        Assert.Equal(0m, resumen.TotalGastos);
    }

    [Fact]
    public async Task NoSePuedeEditarElMovimientoDeOtroUsuario()
    {
        var (clienteA, _) = await _factory.CrearClienteAutenticadoAsync();
        var (clienteB, _) = await _factory.CrearClienteAutenticadoAsync();

        var categoriaA = await clienteA.CrearCategoriaAsync("Mercado", TipoCategoria.Gasto);
        var movA = await clienteA.RegistrarMovimientoAsync(categoriaA.Id, 10_000m);
        var categoriaB = await clienteB.CrearCategoriaAsync("Mercado", TipoCategoria.Gasto);

        var respuesta = await clienteB.PutAsJsonAsync($"/api/movimientos-diaadia/{movA.Id}",
            new ActualizarMovimientoDiaADiaRequest(categoriaB.Id, 999_000m, null, null));

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task NoSePuedeMoverAUnaCategoriaDeOtroUsuario()
    {
        var (clienteA, _) = await _factory.CrearClienteAutenticadoAsync();
        var (clienteB, _) = await _factory.CrearClienteAutenticadoAsync();

        var categoriaA = await clienteA.CrearCategoriaAsync("Mercado", TipoCategoria.Gasto);
        var movA = await clienteA.RegistrarMovimientoAsync(categoriaA.Id, 10_000m);
        var categoriaB = await clienteB.CrearCategoriaAsync("Ajena", TipoCategoria.Gasto);

        var respuesta = await clienteA.PutAsJsonAsync($"/api/movimientos-diaadia/{movA.Id}",
            new ActualizarMovimientoDiaADiaRequest(categoriaB.Id, 10_000m, null, null));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task NoSePuedeDejarUnMontoEnCero()
    {
        var (cliente, _) = await _factory.CrearClienteAutenticadoAsync();
        var categoria = await cliente.CrearCategoriaAsync("Mercado", TipoCategoria.Gasto);
        var mov = await cliente.RegistrarMovimientoAsync(categoria.Id, 10_000m);

        var respuesta = await cliente.PutAsJsonAsync($"/api/movimientos-diaadia/{mov.Id}",
            new ActualizarMovimientoDiaADiaRequest(categoria.Id, 0m, null, null));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }
}
