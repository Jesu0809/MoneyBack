using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using MoneyBack.Api.Data;
using MoneyBack.Api.Dtos;
using MoneyBack.Api.Models.DiaADia;

using MoneyBack.Api.Services;

namespace MoneyBack.Api.Tests;

/// <summary>
/// Desde que el atajo registra solo, nadie mira cuánto lleva gastado: el aviso
/// de tope es la única ocasión en que la persona se entera a tiempo. Estas
/// pruebas verifican las dos mitades de que eso funcione — que avise cuando
/// toca, y que no avise más de una vez, porque quien recibe cinco avisos del
/// mismo tope silencia la app y deja de enterarse de todo lo demás.
///
/// Se comprueba la constancia que queda en AvisosPresupuesto y no el envío en
/// sí: esa fila es exactamente la decisión de avisar, y es lo que impide el
/// segundo aviso.
/// </summary>
public class AlertasPresupuestoTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AlertasPresupuestoTests(ApiFactory factory) => _factory = factory;

    /// <summary>
    /// El calendario de acá, igual que el servicio. Con DateTime.UtcNow,
    /// corriendo de noche el último día del mes la prueba guardaba el tope
    /// en el mes siguiente y el servicio lo buscaba en el actual: no
    /// coincidían y no se disparaba ningún aviso. La prueba tropezaba con
    /// el mismo error que existe para detectar.
    /// </summary>
    private static readonly DateTime Hoy = HoraColombia.Hoy();

    private async Task<(HttpClient Cliente, int UsuarioId, CategoriaResponse Comida)> PrepararAsync(decimal tope)
    {
        var (cliente, usuario) = await _factory.CrearClienteAutenticadoAsync();
        var comida = await cliente.CrearCategoriaAsync("Comida", TipoCategoria.Gasto);

        var guardado = await cliente.PostAsJsonAsync("/api/presupuestos",
            new GuardarPresupuestoRequest(comida.Id, tope, Hoy.Month, Hoy.Year));
        guardado.EnsureSuccessStatusCode();

        return (cliente, usuario.Id, comida);
    }

    private static async Task GastarAsync(HttpClient cliente, CategoriaResponse categoria, decimal monto)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/movimientos-diaadia",
            new CrearMovimientoDiaADiaRequest(categoria.Id, monto, null, null));
        respuesta.EnsureSuccessStatusCode();
    }

    private List<AvisoPresupuesto> AvisosDe(int usuarioId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return db.AvisosPresupuesto.Where(a => a.UsuarioId == usuarioId).ToList();
    }

    [Fact]
    public async Task NoAvisaMientrasElGastoVaPorDebajoDelOchentaPorCiento()
    {
        var (cliente, usuarioId, comida) = await PrepararAsync(tope: 500_000m);

        await GastarAsync(cliente, comida, 300_000m);

        Assert.Empty(AvisosDe(usuarioId));
    }

    [Fact]
    public async Task AvisaUnaSolaVezAlCruzarElOchentaPorCiento()
    {
        var (cliente, usuarioId, comida) = await PrepararAsync(tope: 500_000m);

        await GastarAsync(cliente, comida, 410_000m);
        Assert.Equal(80, Assert.Single(AvisosDe(usuarioId)).Umbral);

        // Sigue gastando dentro del tope: ya lo sabe, no hay nada nuevo que decir.
        await GastarAsync(cliente, comida, 20_000m);
        Assert.Single(AvisosDe(usuarioId));
    }

    /// <summary>
    /// Pasarse sí merece un segundo aviso: es información distinta de "vas en
    /// 80%", y es la que cambia lo que hace la persona el resto del mes.
    /// </summary>
    [Fact]
    public async Task DespuesDeAvisarElOchenta_PasarseAvisaOtraVez()
    {
        var (cliente, usuarioId, comida) = await PrepararAsync(tope: 500_000m);

        await GastarAsync(cliente, comida, 410_000m);
        await GastarAsync(cliente, comida, 200_000m);

        var avisos = AvisosDe(usuarioId).Select(a => a.Umbral).OrderBy(u => u).ToList();
        Assert.Equal([80, 100], avisos);
    }

    /// <summary>
    /// Una sola compra puede saltar del 40% al 130%. Ahí lo que importa es que
    /// se pasó, no que cruzó el 80% en el camino: dos notificaciones seguidas
    /// diciendo casi lo mismo es justo lo que enseña a ignorarlas.
    /// </summary>
    [Fact]
    public async Task UnGastoQueSaltaDirectoPorEncimaDelTope_AvisaSoloQueSePaso()
    {
        var (cliente, usuarioId, comida) = await PrepararAsync(tope: 500_000m);

        await GastarAsync(cliente, comida, 650_000m);

        Assert.Equal(100, Assert.Single(AvisosDe(usuarioId)).Umbral);
    }

    [Fact]
    public async Task SinTopeDefinido_NoAvisaNada()
    {
        var (cliente, usuario) = await _factory.CrearClienteAutenticadoAsync();
        var comida = await cliente.CrearCategoriaAsync("Comida", TipoCategoria.Gasto);

        await GastarAsync(cliente, comida, 900_000m);

        Assert.Empty(AvisosDe(usuario.Id));
    }

    [Fact]
    public async Task UnIngresoNuncaDisparaUnAvisoDeTope()
    {
        var (cliente, usuario) = await _factory.CrearClienteAutenticadoAsync();
        var sueldo = await cliente.CrearCategoriaAsync("Sueldo", TipoCategoria.Ingreso);
        await cliente.PostAsJsonAsync("/api/presupuestos",
            new GuardarPresupuestoRequest(sueldo.Id, 100_000m, Hoy.Month, Hoy.Year));

        await GastarAsync(cliente, sueldo, 3_000_000m);

        Assert.Empty(AvisosDe(usuario.Id));
    }

    [Fact]
    public async Task ElTopeDeUnaPersonaNoDisparaAvisosEnLaCuentaDeOtra()
    {
        var (clienteA, usuarioA, comidaA) = await PrepararAsync(tope: 500_000m);
        var (_, usuarioB, _) = await PrepararAsync(tope: 500_000m);

        await GastarAsync(clienteA, comidaA, 480_000m);

        Assert.Single(AvisosDe(usuarioA));
        Assert.Empty(AvisosDe(usuarioB));
    }

    /// <summary>
    /// El tope se define una vez y sigue rigiendo. Si hubiera que volver a
    /// ponerlo cada mes, en marzo casi nadie tendría topes y los avisos —que
    /// son la razón de ponerlos— se callarían solos.
    /// </summary>
    [Fact]
    public async Task ElTopeSigueVigenteElMesSiguienteSinVolverloAPoner()
    {
        var (cliente, _) = await _factory.CrearClienteAutenticadoAsync();
        var comida = await cliente.CrearCategoriaAsync("Comida", TipoCategoria.Gasto);

        var mesPasado = Hoy.AddMonths(-1);
        await cliente.PostAsJsonAsync("/api/presupuestos",
            new GuardarPresupuestoRequest(comida.Id, 400_000m, mesPasado.Month, mesPasado.Year));

        var vigentes = await cliente.GetFromJsonAsync<List<PresupuestoResponse>>(
            $"/api/presupuestos?mes={Hoy.Month}&anio={Hoy.Year}");

        var tope = Assert.Single(vigentes!);
        Assert.Equal(400_000m, tope.MontoLimite);
        Assert.True(tope.Heredado);
    }
}
