using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using MoneyBack.Web.Services;

namespace MoneyBack.Web.Tests.Infra;

/// <summary>
/// Base para probar pantallas completas: arma el mismo árbol de servicios que
/// Program.cs, pero con el cable hacia el servidor reemplazado por
/// <see cref="ServidorFalso"/>.
///
/// Por qué existe esto: las ~204 pruebas del backend verifican que la plata se
/// calcule bien, y ninguna podía ver que un botón no se renderizara, que un
/// select quedara vacío o que una llave suelta se imprimiera como texto. Todos
/// los errores que se le escaparon a esas pruebas fueron de este tipo.
/// </summary>
public abstract class PruebaDePantalla : TestContext
{
    protected ServidorFalso Servidor { get; }

    protected PruebaDePantalla()
    {
        // Loose: las pantallas llaman a localStorage, al tema y a los push sin
        // que eso sea lo que se está probando. Que devuelvan el valor por
        // defecto en vez de reventar es exactamente lo que se quiere acá.
        JSInterop.Mode = JSRuntimeMode.Loose;

        var estado = new EstadoConexion();
        Servidor = new ServidorFalso { Estado = estado };

        Services.AddSingleton<IHttpClientFactory>(Servidor);
        Services.AddSingleton(estado);
        Services.AddSingleton<TokenStore>();
        Services.AddSingleton<ThemeService>();
        Services.AddSingleton<UiOverlayService>();
        Services.AddSingleton<ConfirmacionService>();
        Services.AddScoped<AuthService>();
        Services.AddScoped<ApiClient>();
        Services.AddScoped<AlmacenLocal>();
        Services.AddScoped<ColaPendientes>();
        Services.AddScoped<DescargaArchivoService>();
        Services.AddScoped<PushNotificationService>();

        Autorizacion = this.AddTestAuthorization();
        Autorizacion.SetAuthorized("Alguien");
    }

    protected TestAuthorizationContext Autorizacion { get; }

    protected FakeNavigationManager Navegador => Services.GetRequiredService<FakeNavigationManager>();

    /// <summary>La ruta a la que la pantalla navegó, sin el origen.</summary>
    protected string RutaActual => Navegador.ToBaseRelativePath(Navegador.Uri);

    protected ConfirmacionService Confirmacion => Services.GetRequiredService<ConfirmacionService>();

    /// <summary>Responde que sí a la hoja de confirmación que esté abierta.</summary>
    protected void ConfirmarLoQuePregunte()
    {
        Assert.NotNull(Confirmacion.Pendiente);
        Confirmacion.Responder(true);
    }
}
