using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using MoneyBack.Api.Config;
using MoneyBack.Api.Data;
using MoneyBack.Api.Endpoints;
using MoneyBack.Api.Models;
using MoneyBack.Api.Services;

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"), npgsql =>
    {
        // Sin esto, cualquier tropiezo momentáneo con la base —un reinicio de
        // Neon, un corte de red de dos segundos, la conexión que se durmió—
        // se convertía en un error para quien estaba usando la app en ese
        // instante. No hay nada roto que arreglar en esos casos: hay que
        // volver a intentar, y eso es justo lo que no se estaba haciendo.
        //
        // Importa el doble acá porque el atajo del banco tiene una sola
        // oportunidad: si su llamada falla, ese gasto no se registra nunca y
        // nadie se entera hasta que cuadra cuentas y no le da.
        npgsql.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorCodesToAdd: null);

        npgsql.CommandTimeout(30);
    }));

builder.Services.Configure<SubsidiosOptions>(builder.Configuration.GetSection(SubsidiosOptions.SectionName));
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<PushOptions>(builder.Configuration.GetSection(PushOptions.SectionName));
builder.Services.AddSingleton<TokenService>();
builder.Services.AddScoped<PushNotificationSender>();
builder.Services.AddHostedService<RevisionSuscripcionesService>();
builder.Services.AddHostedService<ResumenSemanalService>();

builder.Services
    .AddIdentityCore<Usuario>(options =>
    {
        options.Password.RequiredLength = 10;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireDigit = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole<int>>()
    // Sin esto, los errores de Identity salen en inglés y escritos para
    // quien programa: alguien intentando registrarse vio "Username
    // 'correo@gmail.com ' is invalid, can only contain letters or digits".
    .AddErrorDescriber<ErroresEnEspanol>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Falta configurar la sección Jwt.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(jwtOptions.ClaveSecreta)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

var origenesPermitidos = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (origenesPermitidos.Length > 0)
        {
            policy.WithOrigins(origenesPermitidos).AllowAnyHeader().AllowAnyMethod();
        }
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // Particionado por IP, no global. Sin la partición, los 5 intentos por
    // minuto se reparten entre TODOS los usuarios de la app: seis personas
    // entrando a la vez dejaban a la sexta bloqueada sin haber hecho nada
    // raro, y un solo atacante podía cerrarle la puerta a todo el mundo
    // gastándose la cuota. El límite es contra la fuerza bruta de UNA fuente.
    options.AddPolicy("auth", contexto => RateLimitPartition.GetFixedWindowLimiter(
        contexto.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});

builder.Services.AddHealthChecks().AddDbContextCheck<ApplicationDbContext>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseHttpsRedirection();
}
else
{
    app.UseExceptionHandler();
    // Fly.io termina TLS en su borde y reenvía HTTP puro al contenedor;
    // sin esto, UseHttpsRedirection generaría redirects incorrectos.
    var forwardedHeadersOptions = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
    };
    forwardedHeadersOptions.KnownNetworks.Clear();
    forwardedHeadersOptions.KnownProxies.Clear();
    app.UseForwardedHeaders(forwardedHeadersOptions);
}

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");

app.MapAuthEndpoints();
app.MapAdminEndpoints();
app.MapNotificacionesEndpoints();
app.MapHogaresEndpoints();
app.MapInvitacionesHogarEndpoints();
app.MapMetasEndpoints();
app.MapSubsidiosEndpoints();
app.MapCategoriasEndpoints();
app.MapMovimientosDiaADiaEndpoints();
app.MapComerciosAprendidosEndpoints();
app.MapInvitacionesAppEndpoints();
app.MapRecuperacionEndpoints();
app.MapPresupuestosEndpoints();
app.MapSuscripcionesEndpoints();
app.MapDeudasEndpoints();
app.MapReportesEndpoints();
app.MapPushEndpoints();
app.MapTokensAtajoEndpoints();
app.MapAtajosEndpoints();
app.MapTarjetasCreditoEndpoints();

await app.SembrarCodigoInvitacionAsync();
await app.PromoverSuperAdminSiSePidioAsync();
await app.CrearAdminInicialAsync();

app.Run();

/// <summary>Marcador requerido por WebApplicationFactory&lt;Program&gt; en MoneyBack.Api.Tests — Program.cs usa top-level statements, sin esto el tipo no es accesible desde otro ensamblado.</summary>
public partial class Program;
