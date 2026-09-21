using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using MoneyBack.Api.Data;

namespace MoneyBack.Api.Tests;

/// <summary>
/// Levanta la API real (mismos endpoints, mismo pipeline de auth) contra una
/// base de datos EF Core InMemory en vez de la Postgres de producción, y sin
/// los BackgroundService (RevisionSuscripciones/ResumenSemanal correrían en
/// segundo plano y tocarían la base de pruebas de forma impredecible durante
/// las aserciones). Una instancia nueva por clase de test = base de datos
/// nueva y aislada (Guid), así los tests no se pisan entre sí.
///
/// A propósito NO se sobrescribe Jwt:* por config: Program.cs lee esa
/// sección de forma síncrona, ANTES de builder.Build(), para configurar el
/// middleware de JwtBearer — cualquier override inyectado vía
/// ConfigureAppConfiguration solo queda visible después de Build(), así que
/// el middleware validaría contra un secreto distinto al que usa
/// TokenService para firmar (401 garantizado). Dejar la config real
/// (user-secrets del desarrollador) intacta hace que ambos lados usen
/// exactamente el mismo secreto, sin necesidad de replicarlo aquí.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Development" (no "Testing"): así el pipeline expone el detalle
        // real de cualquier excepción en vez del ProblemDetails genérico de
        // producción — necesario para poder diagnosticar un 500 en los tests.
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // Program.cs ya registró AddDbContext(UseNpgsql(...)) antes de
            // que este callback corra. Quitar solo el descriptor de
            // DbContextOptions<T> y volver a llamar AddDbContext(InMemory)
            // deja las dos extensiones (Npgsql + InMemory) conviviendo en el
            // mismo DbContextOptions y EF Core lo rechaza en runtime — hay
            // que registrar las options ya construidas directamente, sin
            // pasar otra vez por AddDbContext.
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            var opciones = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(_dbName)
                .Options;
            services.AddSingleton(opciones);

            // Sin esto, RevisionSuscripcionesService y ResumenSemanalService
            // arrancan con el host y corren su primera pasada de inmediato
            // (patrón do/while), leyendo/escribiendo la base de prueba en
            // paralelo con las aserciones de cada test.
            services.RemoveAll<IHostedService>();
        });
    }
}
