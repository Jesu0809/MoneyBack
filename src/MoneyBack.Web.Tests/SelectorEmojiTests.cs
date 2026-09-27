using Microsoft.AspNetCore.Components;
using MoneyBack.Web.Services;
using MoneyBack.Web.Shared;
using MoneyBack.Web.Tests.Infra;

namespace MoneyBack.Web.Tests;

/// <summary>
/// El selector de íconos y el diccionario que lo alimenta. Nadie escribe un
/// emoji: se toca uno de los sugeridos, así que si las sugerencias no
/// aciertan, el componente no sirve para nada aunque se renderice bien.
/// </summary>
public class SelectorEmojiTests : PruebaDePantalla
{
    [Fact]
    public void MuestraSieteSugeridosMasElBotonDeVerTodos()
    {
        var selector = RenderComponent<SelectorEmoji>(p => p
            .Add(s => s.Valor, "🏠")
            .Add(s => s.Sugeridos, ["🏠", "🏡", "🔑", "🛋", "🧱", "🚪", "🪟", "🏗", "🏘"]));

        // Siete sugeridos: el noveno y el octavo no caben, y el último botón
        // es el que abre el catálogo completo.
        Assert.Equal(8, selector.FindAll("button.emoji-chip").Count);
        Assert.Single(selector.FindAll("button.emoji-chip-mas"));
    }

    [Fact]
    public void ElEmojiElegidoQuedaMarcado()
    {
        var selector = RenderComponent<SelectorEmoji>(p => p
            .Add(s => s.Valor, "🔑")
            .Add(s => s.Sugeridos, ["🏠", "🔑", "🏡"]));

        Assert.Equal("🔑", selector.Find("button.emoji-chip.active").TextContent);
    }

    [Fact]
    public void TocarUnSugeridoAvisaCualSeEligio()
    {
        string? elegido = null;
        var selector = RenderComponent<SelectorEmoji>(p => p
            .Add(s => s.Valor, "🏠")
            .Add(s => s.Sugeridos, ["🏠", "🔑", "🏡"])
            .Add(s => s.ValorChanged, EventCallback.Factory.Create<string>(this, v => elegido = v)));

        selector.FindAll("button.emoji-chip")[1].Click();

        Assert.Equal("🔑", elegido);
    }

    [Fact]
    public void ElCatalogoCompletoSeAbreYSeCierra()
    {
        var selector = RenderComponent<SelectorEmoji>(p => p.Add(s => s.Valor, "🏠"));

        Assert.Empty(selector.FindAll(".fullscreen-modal"));
        selector.Find("button.emoji-chip-mas").Click();
        Assert.Single(selector.FindAll(".fullscreen-modal"));

        selector.Find(".fullscreen-header button").Click();
        Assert.Empty(selector.FindAll(".fullscreen-modal"));
    }

    // --- El diccionario que da las sugerencias ---

    [Theory]
    [InlineData("apartamento", "🏠")]
    [InlineData("carro", "🚗")]
    [InlineData("mercado", "🛒")]
    [InlineData("viaje", "✈️")]
    [InlineData("tinto", "☕")]
    [InlineData("gato", "🐈")]
    public void LasSugerenciasAciertanConLaPrimeraPalabraObvia(string texto, string esperado)
    {
        Assert.Contains(esperado, EmojisSugeridos.Para(texto));
    }

    /// <summary>
    /// Sin tilde tiene que dar lo mismo: nadie escribe "avión" con tilde en
    /// el teclado del celular, y antes eso hacía que no saliera nada.
    /// </summary>
    [Theory]
    [InlineData("avion", "avión")]
    [InlineData("musica", "música")]
    [InlineData("matricula", "matrícula")]
    public void LasTildesNoCambianElResultado(string sinTilde, string conTilde)
    {
        Assert.Equal(EmojisSugeridos.Para(conTilde), EmojisSugeridos.Para(sinTilde));
    }

    /// <summary>
    /// Con el campo vacío hay que ofrecer algo igual: un selector sin
    /// opciones deja a la persona sin nada que tocar.
    /// </summary>
    [Fact]
    public void ConElNombreVacioIgualOfreceOpciones()
    {
        Assert.NotEmpty(EmojisSugeridos.Para(""));
        Assert.NotEmpty(EmojisSugeridos.Para("   "));
    }

    /// <summary>
    /// Un nombre que no se parece a nada no puede devolver una lista vacía
    /// ni menos de siete: el componente muestra lo que le llegue.
    /// </summary>
    [Fact]
    public void UnNombreSinSentidoIgualDevuelveSieteOpciones()
    {
        var sugeridos = EmojisSugeridos.Para("xqzptr");
        Assert.True(sugeridos.Length >= 7, $"Solo devolvió {sugeridos.Length}.");
    }

    /// <summary>
    /// "Bogotá" daba 💧 (por "gota") y "gaseosa" daba 🚻 (por "aseo"): el
    /// rescate por subcadena solo debe usarse cuando no hubo nada mejor.
    /// </summary>
    [Theory]
    [InlineData("Bogotá", "💧")]
    [InlineData("gaseosa", "🚻")]
    [InlineData("Cartagena", "♠")]
    public void NoSugiereEmojisPorPedazosDePalabra(string texto, string absurdo)
    {
        Assert.DoesNotContain(absurdo, EmojisSugeridos.Para(texto).Take(3));
    }
}
