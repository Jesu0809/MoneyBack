using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using MoneyBack.Api.Models;
using MoneyBack.Api.Models.Auth;

namespace MoneyBack.Api.Tests;

/// <summary>
/// El arranque que crea la cuenta de administración.
///
/// Lo delicado no es crearla: es que un ajuste olvidado en la configuración
/// no le reescriba la contraseña a nadie en cada reinicio de la máquina.
/// </summary>
public class AdminInicialTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AdminInicialTests(ApiFactory factory) => _factory = factory;

    /// <summary>
    /// Simula lo que hace el arranque, sobre el mismo Identity que usa la
    /// app: crear si no existe, y solo asegurar el rol si ya existía.
    /// </summary>
    private async Task<(bool Creada, Usuario Usuario)> ArrancarConAsync(string correo, string clave)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();

        if (!await roleManager.RoleExistsAsync(Roles.SuperAdmin))
        {
            await roleManager.CreateAsync(new IdentityRole<int>(Roles.SuperAdmin));
        }

        var existente = await userManager.FindByEmailAsync(correo);
        if (existente is not null)
        {
            if (!await userManager.IsInRoleAsync(existente, Roles.SuperAdmin))
            {
                await userManager.AddToRoleAsync(existente, Roles.SuperAdmin);
            }
            return (false, existente);
        }

        var admin = new Usuario { UserName = correo, Email = correo, Nombre = "Administración", EmailConfirmed = true };
        var resultado = await userManager.CreateAsync(admin, clave);
        Assert.True(resultado.Succeeded, string.Join(", ", resultado.Errors.Select(e => e.Description)));
        await userManager.AddToRoleAsync(admin, Roles.SuperAdmin);
        return (true, admin);
    }

    [Fact]
    public async Task LaCuentaNuevaNaceConElRolYPuedeEntrar()
    {
        var correo = $"admin-{Guid.NewGuid():N}@moneyback.app";
        var (creada, usuario) = await ArrancarConAsync(correo, "ClaveDeAdmin#2026");

        Assert.True(creada);

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        var recargado = await userManager.FindByEmailAsync(correo);

        Assert.Contains(Roles.SuperAdmin, await userManager.GetRolesAsync(recargado!));
        Assert.True(await userManager.CheckPasswordAsync(recargado!, "ClaveDeAdmin#2026"));
    }

    /// <summary>
    /// Lo más importante de todo: si el ajuste se queda puesto, cada
    /// reinicio de la máquina NO puede volver a poner la contraseña
    /// original. Quien ya se la cambió por la suya la perdería sin
    /// enterarse, y la vieja —que puede estar en un chat— volvería a servir.
    /// </summary>
    [Fact]
    public async Task ArrancarDosVeces_NoLeReescribeLaContrasenaAQuienYaLaCambio()
    {
        var correo = $"admin-{Guid.NewGuid():N}@moneyback.app";
        await ArrancarConAsync(correo, "ClaveOriginal#2026");

        // La persona entra y se pone la suya.
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
            var usuario = await userManager.FindByEmailAsync(correo);
            var token = await userManager.GeneratePasswordResetTokenAsync(usuario!);
            await userManager.ResetPasswordAsync(usuario!, token, "LaMiaPropia#2026");
        }

        // La máquina se reinicia con el ajuste todavía puesto.
        var (creada, _) = await ArrancarConAsync(correo, "ClaveOriginal#2026");
        Assert.False(creada);

        using var verificacion = _factory.Services.CreateScope();
        var um = verificacion.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        var final = await um.FindByEmailAsync(correo);

        Assert.True(await um.CheckPasswordAsync(final!, "LaMiaPropia#2026"));
        Assert.False(await um.CheckPasswordAsync(final!, "ClaveOriginal#2026"));
    }

    /// <summary>
    /// Si el correo apunta a una cuenta de persona que ya existe, se le da
    /// el rol y no se le toca nada más.
    /// </summary>
    [Fact]
    public async Task SiElCorreoYaEsDeAlguien_SoloLeDaElRol()
    {
        var correo = $"{Guid.NewGuid():N}@test.moneyback";
        var (_, usuario) = await _factory.CrearClienteAutenticadoAsync(correo, "Persona real");

        var (creada, _) = await ArrancarConAsync(correo, "OtraClave#2026");
        Assert.False(creada);

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        var recargado = await userManager.FindByEmailAsync(correo);

        Assert.Equal("Persona real", recargado!.Nombre);
        Assert.Contains(Roles.SuperAdmin, await userManager.GetRolesAsync(recargado));
        // La contraseña con la que se creó sigue siendo la suya.
        Assert.True(await userManager.CheckPasswordAsync(recargado, "ClaveSegura#2026"));
    }
}
