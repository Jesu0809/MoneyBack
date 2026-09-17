using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MoneyBack.Api.Config;
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
}
