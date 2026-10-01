using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using MoneyBack.Api.Data;
using MoneyBack.Api.Dtos;
using MoneyBack.Api.Models;
using MoneyBack.Api.Models.Auth;
using MoneyBack.Api.Models.DiaADia;

namespace MoneyBack.Api.Tests;

/// <summary>
/// Borrar una cuenta es la única acción del panel que no se puede deshacer
/// de ninguna forma. Lo que se prueba acá no es que borre —eso es fácil—
/// sino que se niegue a hacerlo cuando dejaría a alguien más con las
/// cuentas cambiadas o al sistema sin administrador.
/// </summary>
public class BorrarCuentaTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public BorrarCuentaTests(ApiFactory factory) => _factory = factory;

    private async Task DarleElRolAsync(int usuarioId)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
        if (!await roleManager.RoleExistsAsync(Roles.SuperAdmin))
        {
            await roleManager.CreateAsync(new IdentityRole<int>(Roles.SuperAdmin));
        }
        var usuario = await userManager.FindByIdAsync(usuarioId.ToString());
        await userManager.AddToRoleAsync(usuario!, Roles.SuperAdmin);
    }

    [Fact]
    public async Task BorraLaCuentaYTodoLoSuyo()
    {
        var (admin, _) = await _factory.CrearAdminAutenticadoAsync();
        var (victima, usuario) = await _factory.CrearClienteAutenticadoAsync();
        var categoria = await victima.CrearCategoriaAsync("Mercado", TipoCategoria.Gasto);
        await victima.RegistrarMovimientoAsync(categoria.Id, 50_000m);

        var respuesta = await admin.DeleteAsync($"/api/admin/usuarios/{usuario.Id}");
        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.False(db.Users.Any(u => u.Id == usuario.Id));
        Assert.False(db.MovimientosDiaADia.Any(m => m.UsuarioId == usuario.Id));
        Assert.False(db.Categorias.Any(c => c.UsuarioId == usuario.Id));
        Assert.False(db.RefreshTokens.Any(t => t.UsuarioId == usuario.Id));
    }

    /// <summary>
    /// La vista previa tiene que decir lo que hay antes de que alguien
    /// decida. Un botón de borrar sin números es un botón que no se puede
    /// evaluar.
    /// </summary>
    [Fact]
    public async Task LaVistaPreviaCuentaLoQueSeVaAPerder()
    {
        var (admin, _) = await _factory.CrearAdminAutenticadoAsync();
        var (victima, usuario) = await _factory.CrearClienteAutenticadoAsync();
        var categoria = await victima.CrearCategoriaAsync("Mercado", TipoCategoria.Gasto);
        await victima.RegistrarMovimientoAsync(categoria.Id, 50_000m);
        await victima.RegistrarMovimientoAsync(categoria.Id, 30_000m);

        var previa = await admin.GetFromJsonAsync<QueSeBorrariaResponse>(
            $"/api/admin/usuarios/{usuario.Id}/que-se-borraria");

        Assert.NotNull(previa);
        Assert.Equal(2, previa!.Movimientos);
        Assert.True(previa.Categorias >= 1);
        Assert.Null(previa.Bloqueo);
    }

    [Fact]
    public async Task NadieBorraSuPropiaCuenta()
    {
        var (admin, yo) = await _factory.CrearAdminAutenticadoAsync();

        var previa = await admin.GetFromJsonAsync<QueSeBorrariaResponse>(
            $"/api/admin/usuarios/{yo.Id}/que-se-borraria");
        Assert.Contains("tu propia cuenta", previa!.Bloqueo);

        var respuesta = await admin.DeleteAsync($"/api/admin/usuarios/{yo.Id}");
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    /// <summary>
    /// Borrar al último administrador dejaría el sistema sin nadie que pueda
    /// administrar: la única salida sería un secreto de Fly y un reinicio.
    /// </summary>
    [Fact]
    public async Task NoSeBorraLaUnicaCuentaAdministradora()
    {
        // El admin que pide y el objetivo son la misma persona en el sentido
        // que importa: si solo hay uno con el rol en la base, no se puede ir.
        var (_, unico) = await _factory.CrearClienteAutenticadoAsync();
        await DarleElRolAsync(unico.Id);

        var (admin, quienPide) = await _factory.CrearAdminAutenticadoAsync();

        // El que pide tiene el rol solo en su token, no en la base, así que
        // en la base el único administrador real es "unico".
        var previa = await admin.GetFromJsonAsync<QueSeBorrariaResponse>(
            $"/api/admin/usuarios/{unico.Id}/que-se-borraria");

        Assert.Contains("única cuenta administradora", previa!.Bloqueo);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.DeleteAsync($"/api/admin/usuarios/{unico.Id}")).StatusCode);
    }

    /// <summary>
    /// Lo más importante: si aportó plata a una meta compartida, borrarla le
    /// cambiaría el total a la otra persona sin avisarle. Eso no se hace
    /// desde un panel.
    /// </summary>
    [Fact]
    public async Task NoSeBorraQuienAportoAUnaMetaCompartida()
    {
        var (admin, _) = await _factory.CrearAdminAutenticadoAsync();
        var (clienteA, clienteB, hogar) = await _factory.CrearGrupoDeDosAsync();

        var crearMeta = await clienteA.PostAsJsonAsync($"/api/hogares/{hogar.Id}/metas",
            new CrearMetaRequest("Apto", 50_000_000m, "🏠", false, 100));
        var meta = (await crearMeta.Content.ReadFromJsonAsync<MetaResponse>())!;

        await clienteB.PostAsJsonAsync($"/api/metas/{meta.Id}/movimientos",
            new CrearMovimientoRequest(MoneyBack.Api.Models.Metas.TipoMovimiento.Aporte, 1_000_000m, "Mi parte"));

        var segundo = hogar.Miembros.Last().UsuarioId;

        var previa = await admin.GetFromJsonAsync<QueSeBorrariaResponse>(
            $"/api/admin/usuarios/{segundo}/que-se-borraria");
        Assert.Contains("metas compartidas", previa!.Bloqueo);

        var respuesta = await admin.DeleteAsync($"/api/admin/usuarios/{segundo}");
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(db.Users.Any(u => u.Id == segundo));
    }

    /// <summary>
    /// Un grupo donde era el último miembro queda sin nadie: se va con sus
    /// metas. Dejarlo huérfano sería basura invisible que nadie puede
    /// alcanzar ni borrar.
    /// </summary>
    [Fact]
    public async Task ElGrupoDondeEraElUnicoMiembroSeVaConLaCuenta()
    {
        var (admin, _) = await _factory.CrearAdminAutenticadoAsync();
        var (victima, usuario) = await _factory.CrearClienteAutenticadoAsync();

        var crear = await victima.PostAsJsonAsync("/api/hogares", new CrearHogarRequest("Solo mío", false));
        var grupo = (await crear.Content.ReadFromJsonAsync<HogarResponse>())!;
        await victima.PostAsJsonAsync($"/api/hogares/{grupo.Id}/metas",
            new CrearMetaRequest("Lavadora", 2_000_000m, "🧺", false, 0));

        var previa = await admin.GetFromJsonAsync<QueSeBorrariaResponse>(
            $"/api/admin/usuarios/{usuario.Id}/que-se-borraria");
        Assert.Contains("Solo mío", previa!.GruposQueSeBorran);

        (await admin.DeleteAsync($"/api/admin/usuarios/{usuario.Id}")).EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(db.Hogares.Any(h => h.Id == grupo.Id));
        Assert.False(db.MetasAhorro.Any(m => m.HogarId == grupo.Id));
    }

    [Fact]
    public async Task UnUsuarioNormalNoBorraANadie()
    {
        var (cliente, _) = await _factory.CrearClienteAutenticadoAsync();
        var (_, victima) = await _factory.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.DeleteAsync($"/api/admin/usuarios/{victima.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task UnaCuentaQueNoExisteDaNoEncontrado()
    {
        var (admin, _) = await _factory.CrearAdminAutenticadoAsync();

        Assert.Equal(HttpStatusCode.NotFound,
            (await admin.DeleteAsync("/api/admin/usuarios/999999")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await admin.GetAsync("/api/admin/usuarios/999999/que-se-borraria")).StatusCode);
    }
}
