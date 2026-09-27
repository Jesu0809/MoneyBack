using System.Net;
using MoneyBack.Web.Models;
using MoneyBack.Web.Pages;
using MoneyBack.Web.Tests.Infra;

namespace MoneyBack.Web.Tests;

public class CategoriasPantallaTests : PruebaDePantalla
{
    private void ServidorBase(params CategoriaResponse[] categorias) => Servidor
        .Responde("GET", "api/categorias", categorias)
        .Responde("GET", "api/comercios-aprendidos", Array.Empty<object>());

    [Fact]
    public void SeparaGastosDeIngresos()
    {
        ServidorBase(
            Datos.Categoria(1, "Mercado", "🛒"),
            Datos.Categoria(2, "Sueldo", "💼", TipoCategoria.Ingreso));

        var pantalla = RenderComponent<Categorias>();

        Assert.Contains("Gastos", pantalla.Markup);
        Assert.Contains("Ingresos", pantalla.Markup);
        Assert.Contains("Mercado", pantalla.Markup);
        Assert.Contains("Sueldo", pantalla.Markup);
    }

    /// <summary>
    /// Escribir el nombre reventaba la pantalla: el campo usaba una sintaxis
    /// de binding que no existe para componentes y fallaba en tiempo de
    /// ejecución, no al compilar.
    /// </summary>
    [Fact]
    public void EscribirElNombreNoRompeLaPantalla()
    {
        ServidorBase();
        var pantalla = RenderComponent<Categorias>();
        pantalla.Find("button.btn-primary").Click();

        pantalla.Find("input.input").Input("Mascotas");
        pantalla.Find("input.input").Input("Mascotas y veterinario");

        Assert.Equal("Mascotas y veterinario", pantalla.Find("input.input").GetAttribute("value"));
    }

    /// <summary>
    /// Lo que se escribe tiene que cambiar los íconos sugeridos en el acto —
    /// esa es toda la gracia del selector.
    /// </summary>
    [Fact]
    public void EscribirElNombreCambiaLosIconosSugeridos()
    {
        ServidorBase();
        var pantalla = RenderComponent<Categorias>();
        pantalla.Find("button.btn-primary").Click();

        pantalla.Find("input.input").Input("mascotas");
        var conMascotas = pantalla.FindAll("button.emoji-chip").Select(c => c.TextContent).ToList();

        pantalla.Find("input.input").Input("gasolina");
        var conGasolina = pantalla.FindAll("button.emoji-chip").Select(c => c.TextContent).ToList();

        Assert.NotEqual(conMascotas, conGasolina);
    }

    [Fact]
    public void CrearCategoria_MandaNombreTipoEIcono()
    {
        ServidorBase();
        Servidor.Responde("POST", "api/categorias", Datos.Categoria(9, "Mascotas", "🐾"), HttpStatusCode.Created);

        var pantalla = RenderComponent<Categorias>();
        pantalla.Find("button.btn-primary").Click();
        pantalla.Find("input.input").Input("Mascotas");
        pantalla.FindAll("button.emoji-chip")[0].Click();
        pantalla.Find("form").Submit();

        var llamada = Servidor.Llamadas.Last(l => l is { Metodo: "POST", Ruta: "api/categorias" });
        Assert.Contains("\"nombre\":\"Mascotas\"", llamada.Cuerpo);
        Assert.Contains("\"tipo\"", llamada.Cuerpo);
    }

    /// <summary>
    /// Una categoría archivada se sigue viendo (con su historial detrás) pero
    /// apagada, y se puede reactivar. Borrarla se llevaría los gastos.
    /// </summary>
    [Fact]
    public void UnaCategoriaArchivadaSeVeApagadaYSePuedeReactivar()
    {
        ServidorBase(Datos.Categoria(1, "Antojos", "🍫", activa: false));

        var pantalla = RenderComponent<Categorias>();

        Assert.Contains("Reactivar", pantalla.Markup);
        Assert.Contains("opacity:0.4", pantalla.Markup);
    }

    [Fact]
    public void LosSitiosAprendidosSeListanYSePuedenOlvidar()
    {
        Servidor
            .Responde("GET", "api/categorias", new[] { Datos.Categoria() })
            .Responde("GET", "api/comercios-aprendidos", new[]
            {
                new { id = 1, comercio = "EXITO CHAPINERO", categoriaId = 100, categoriaNombre = "Mercado", categoriaIcono = "🛒" }
            });

        var pantalla = RenderComponent<Categorias>();

        Assert.Contains("EXITO CHAPINERO", pantalla.Markup);
        Assert.Contains("entra como Mercado", pantalla.Markup);
    }

    [Fact]
    public void SiElServidorRechazaLaCategoria_SeVeElMotivo()
    {
        ServidorBase();
        Servidor.Falla("POST", "api/categorias", HttpStatusCode.Conflict, "Ya tienes una categoría con ese nombre.");

        var pantalla = RenderComponent<Categorias>();
        pantalla.Find("button.btn-primary").Click();
        pantalla.Find("input.input").Input("Mercado");
        pantalla.Find("form").Submit();

        Assert.Contains("Ya tienes una categoría con ese nombre.", pantalla.Markup);
    }
}
