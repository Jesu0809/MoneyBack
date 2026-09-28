using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MoneyBack.Api.Config;
using Microsoft.AspNetCore.Identity;
using MoneyBack.Api.Models;
using MoneyBack.Api.Models.Auth;
using MoneyBack.Api.Services;

namespace MoneyBack.Api.Data;

public static class SeedInicial
{
    /// <summary>
    /// Siembra el primer código de invitación si la tabla está vacía. Después
    /// de esto, el SuperAdmin lo rota desde /api/admin sin volver a tocar
    /// esta configuración ni reiniciar la app.
    /// </summary>
    public static async Task SembrarCodigoInvitacionAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var authOptions = scope.ServiceProvider.GetRequiredService<IOptions<AuthOptions>>().Value;

        if (await db.CodigosInvitacion.AnyAsync()) return;

        if (string.IsNullOrWhiteSpace(authOptions.CodigoInvitacionInicial))
        {
            app.Logger.LogWarning(
                "Auth:CodigoInvitacionInicial no está configurado y no hay ningún código de invitación en la base de datos. " +
                "Nadie podrá registrarse hasta que un SuperAdmin cree uno (pero tampoco hay SuperAdmin todavía).");
            return;
        }

        db.CodigosInvitacion.Add(new CodigoInvitacion
        {
            CodigoHash = TokenService.HashearToken(authOptions.CodigoInvitacionInicial)
        });
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Le da el rol SuperAdmin a la cuenta que diga Auth:PromoverASuperAdmin,
    /// si es que dice alguna.
    ///
    /// Es la única forma de recuperar la administración sin acceso a la base:
    /// el rol se le da a la primera cuenta registrada y, si se pierde esa
    /// cuenta, no queda nadie que pueda dárselo a otra.
    ///
    /// No crea cuentas ni toca contraseñas. Si el correo no existe, lo dice
    /// en el registro y sigue — arrancar la app no puede depender de que un
    /// ajuste de emergencia esté bien escrito.
    /// </summary>
    public static async Task PromoverSuperAdminSiSePidioAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var authOptions = scope.ServiceProvider.GetRequiredService<IOptions<AuthOptions>>().Value;

        var correo = authOptions.PromoverASuperAdmin?.Trim();
        if (string.IsNullOrWhiteSpace(correo)) return;

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();

        var usuario = await userManager.FindByEmailAsync(correo);
        if (usuario is null)
        {
            app.Logger.LogWarning(
                "Auth:PromoverASuperAdmin apunta a un correo que no está registrado. No se promovió a nadie.");
            return;
        }

        if (!await roleManager.RoleExistsAsync(Roles.SuperAdmin))
        {
            await roleManager.CreateAsync(new IdentityRole<int>(Roles.SuperAdmin));
        }

        if (await userManager.IsInRoleAsync(usuario, Roles.SuperAdmin))
        {
            app.Logger.LogWarning(
                "Auth:PromoverASuperAdmin sigue configurado y esa cuenta ya es SuperAdmin. Conviene quitar el ajuste.");
            return;
        }

        await userManager.AddToRoleAsync(usuario, Roles.SuperAdmin);

        // Como advertencia y no como información: esto no debería pasar en
        // una operación normal, y si aparece en los registros sin que nadie
        // lo haya pedido, hay que mirarlo.
        app.Logger.LogWarning(
            "Se le dio el rol SuperAdmin a la cuenta {UsuarioId} por Auth:PromoverASuperAdmin. Quitar el ajuste.",
            usuario.Id);
    }

    /// <summary>
    /// Crea la cuenta de administración si hace falta, con el rol puesto.
    ///
    /// No pasa por /registro a propósito: ese camino pide código de
    /// invitación, crea categorías predefinidas y le daría el rol solo si
    /// fuera la primera cuenta del sistema. Esto es otra cosa — una cuenta
    /// de administración, no una persona usando la app.
    ///
    /// Es idempotente y conservador: si el correo ya existe, no le toca la
    /// contraseña, solo se asegura del rol. Un ajuste que quede olvidado en
    /// la configuración no puede reescribirle la clave a nadie en cada
    /// reinicio de la máquina.
    /// </summary>
    public static async Task CrearAdminInicialAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var authOptions = scope.ServiceProvider.GetRequiredService<IOptions<AuthOptions>>().Value;

        var correo = authOptions.AdminInicialEmail?.Trim();
        var clave = authOptions.AdminInicialPassword;

        if (string.IsNullOrWhiteSpace(correo)) return;

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
                app.Logger.LogWarning(
                    "Auth:AdminInicialEmail apunta a una cuenta que ya existía; se le dio el rol SuperAdmin.");
            }
            else
            {
                app.Logger.LogWarning(
                    "Auth:AdminInicialEmail sigue configurado y esa cuenta ya está lista. Conviene quitar el ajuste.");
            }
            return;
        }

        if (string.IsNullOrWhiteSpace(clave))
        {
            app.Logger.LogWarning(
                "Auth:AdminInicialEmail está configurado pero falta Auth:AdminInicialPassword. No se creó nada.");
            return;
        }

        var admin = new Usuario
        {
            UserName = correo,
            Email = correo,
            Nombre = "Administración",
            EmailConfirmed = true
        };

        var resultado = await userManager.CreateAsync(admin, clave);
        if (!resultado.Succeeded)
        {
            // Sin los detalles del error: van al registro del servidor y ahí
            // podrían quedar pistas de la contraseña que se intentó usar.
            app.Logger.LogWarning(
                "No se pudo crear la cuenta de Auth:AdminInicialEmail: la contraseña no cumple las reglas.");
            return;
        }

        await userManager.AddToRoleAsync(admin, Roles.SuperAdmin);

        app.Logger.LogWarning(
            "Se creó la cuenta de administración {UsuarioId} por Auth:AdminInicialEmail. Quitar el ajuste.",
            admin.Id);
    }
}