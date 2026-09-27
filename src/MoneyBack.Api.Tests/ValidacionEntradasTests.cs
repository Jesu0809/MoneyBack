using System.Net;
using System.Net.Http.Json;
using MoneyBack.Api.Dtos;
using MoneyBack.Api.Models.DiaADia;
using MoneyBack.Api.Services;

namespace MoneyBack.Api.Tests;

/// <summary>
/// Lo que la gente escribe llega sin filtrar y el API lo guardaba tal cual:
/// una frase entera como "ícono", un nombre de cinco mil caracteres, una
/// meta sin nombre. Nada de eso rompe la base, pero sí la pantalla — y el
/// mismo texto viaja después al atajo y a las notificaciones.
/// </summary>
public class ValidacionEntradasTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ValidacionEntradasTests(ApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData("🏠", "🏠")]
    [InlineData("🧺 lavadora", "🧺")]   // se queda con el emoji y bota el resto
    [InlineData("🇨🇴", "🇨🇴")]           // bandera: dos símbolos que son uno solo
    public void UnEmojiValidoSeAcepta(string entrada, string esperado) =>
        Assert.Equal(esperado, Emoji.Primero(entrada));

    [Theory]
    [InlineData("esto no es un emoji sino una frase entera")]
    [InlineData("Mercado")]
    [InlineData("123")]
    [InlineData("   ")]
    [InlineData("")]
    [InlineData(null)]
    public void LoQueNoEsEmojiSeRechaza(string? entrada) =>
        Assert.Null(Emoji.Primero(entrada));

    [Fact]
    public async Task UnaCategoriaConFraseDeIconoSeGuardaConElIconoPorDefecto()
    {
        var (cliente, _) = await _factory.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync("/api/categorias",
            new CrearCategoriaRequest("Mercado", TipoCategoria.Gasto, "esto es una frase entera"));

        var categoria = (await respuesta.Content.ReadFromJsonAsync<CategoriaResponse>())!;
        Assert.Equal("📦", categoria.Icono);
    }

    [Fact]
    public async Task UnaMetaSinNombreNoSeCrea()
    {
        var (cliente, hogar) = await CrearHogarAsync();

        var respuesta = await cliente.PostAsJsonAsync($"/api/hogares/{hogar.Id}/metas",
            new CrearMetaRequest("   ", 1_000_000m));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    /// <summary>
    /// Un 99999% no rompe el reparto —se normaliza— pero deja un dato que no
    /// significa nada y que sorprende a quien lo lea después.
    /// </summary>
    [Fact]
    public async Task ElPorcentajeDelVueltoSeAcotaACienPorCiento()
    {
        var (cliente, hogar) = await CrearHogarAsync();

        var respuesta = await cliente.PostAsJsonAsync($"/api/hogares/{hogar.Id}/metas",
            new CrearMetaRequest("Rara", 1_000_000m, "🎯", false, 99_999));

        var meta = (await respuesta.Content.ReadFromJsonAsync<MetaResponse>())!;
        Assert.Equal(100m, meta.PorcentajeRedondeo);
    }

    [Fact]
    public async Task UnPorcentajeNegativoQuedaEnCero()
    {
        var (cliente, hogar) = await CrearHogarAsync();

        var respuesta = await cliente.PostAsJsonAsync($"/api/hogares/{hogar.Id}/metas",
            new CrearMetaRequest("Rara", 1_000_000m, "🎯", false, -40));

        var meta = (await respuesta.Content.ReadFromJsonAsync<MetaResponse>())!;
        Assert.Equal(0m, meta.PorcentajeRedondeo);
    }

    private async Task<(HttpClient Cliente, HogarResponse Hogar)> CrearHogarAsync()
    {
        var (clienteA, _, hogar) = await _factory.CrearGrupoDeDosAsync();
        return (clienteA, hogar);
    }
}
