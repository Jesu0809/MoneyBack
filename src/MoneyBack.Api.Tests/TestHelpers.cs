using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using MoneyBack.Api.Dtos;
using MoneyBack.Api.Models;
using MoneyBack.Api.Services;

namespace MoneyBack.Api.Tests;

public static class TestHelpers
{
    /// <summary>
    /// Crea un usuario directo en Identity (sin pasar por /registro ni el
    /// código de invitación — esa es una regla de negocio aparte, no algo
    /// que la lógica de dinero necesite repetir en cada test) y devuelve un
    /// HttpClient ya autenticado con un access token real, firmado con la
    /// misma llave que usa el pipeline de auth de la factory.
    /// </summary>
    public static async Task<(HttpClient Cliente, Usuario Usuario)> CrearClienteAutenticadoAsync(
        this ApiFactory factory, string? email = null, string nombre = "Usuario de prueba")
    {
        email ??= $"{Guid.NewGuid():N}@test.moneyback";

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        var tokenService = scope.ServiceProvider.GetRequiredService<TokenService>();

        var usuario = new Usuario { UserName = email, Email = email, Nombre = nombre };
        var resultado = await userManager.CreateAsync(usuario, "ClaveSegura#2026");
        if (!resultado.Succeeded)
        {
            throw new InvalidOperationException(string.Join(", ", resultado.Errors.Select(e => e.Description)));
        }

        var token = tokenService.GenerarAccessToken(usuario, []);

        var cliente = factory.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (cliente, usuario);
    }

    public static async Task<CategoriaResponse> CrearCategoriaAsync(this HttpClient cliente, string nombre, MoneyBack.Api.Models.DiaADia.TipoCategoria tipo)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/categorias", new CrearCategoriaRequest(nombre, tipo, "📦"));
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<CategoriaResponse>())!;
    }

    public static async Task<MovimientoDiaADiaResponse> RegistrarMovimientoAsync(
        this HttpClient cliente, int categoriaId, decimal monto, DateTime? fecha = null, int? tarjetaCreditoId = null, string? nota = null)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/movimientos-diaadia",
            new CrearMovimientoDiaADiaRequest(categoriaId, monto, fecha, nota, tarjetaCreditoId));
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<MovimientoDiaADiaResponse>())!;
    }

    /// <summary>
    /// Crea un grupo y le suma una segunda persona.
    ///
    /// Antes un grupo nacía al aceptar una invitación, así que no se podía
    /// tener uno solo. Ahora se crea primero y se invita después, que es el
    /// orden que permite ahorrar solo y sumar gente cuando toque.
    /// </summary>
    public static async Task<(HttpClient Cliente, HttpClient ClienteB, HogarResponse Hogar)>
        CrearGrupoDeDosAsync(this ApiFactory factory, string nombre = "Nosotros")
    {
        var (clienteA, _) = await factory.CrearClienteAutenticadoAsync();
        var (clienteB, usuarioB) = await factory.CrearClienteAutenticadoAsync();

        var creado = await clienteA.PostAsJsonAsync("/api/hogares", new CrearHogarRequest(nombre));
        creado.EnsureSuccessStatusCode();
        var hogar = (await creado.Content.ReadFromJsonAsync<HogarResponse>())!;

        var invitar = await clienteA.PostAsJsonAsync("/api/invitaciones-hogar",
            new CrearInvitacionHogarRequest(usuarioB.Email!, hogar.Id));
        invitar.EnsureSuccessStatusCode();
        var invitacion = (await invitar.Content.ReadFromJsonAsync<InvitacionHogarResponse>())!;

        var aceptar = await clienteB.PostAsync($"/api/invitaciones-hogar/{invitacion.Id}/aceptar", null);
        aceptar.EnsureSuccessStatusCode();

        return (clienteA, clienteB, (await aceptar.Content.ReadFromJsonAsync<HogarResponse>())!);
    }
}
