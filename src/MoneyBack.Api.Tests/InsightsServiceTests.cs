using Microsoft.EntityFrameworkCore;
using MoneyBack.Api.Data;
using MoneyBack.Api.Models;
using MoneyBack.Api.Models.DiaADia;
using MoneyBack.Api.Services;

namespace MoneyBack.Api.Tests;

/// <summary>
/// InsightsService.CalcularTipAsync es puramente reglas sobre datos propios
/// (nada de red ni de auth), así que se prueba directo contra un
/// ApplicationDbContext InMemory sembrado a mano — no hace falta levantar
/// toda la API para esto.
/// </summary>
public class InsightsServiceTests
{
    private static ApplicationDbContext NuevoContexto()
    {
        var opciones = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(opciones);
    }

    private static async Task<Usuario> CrearUsuarioAsync(ApplicationDbContext db, int? diaPago1 = null, int? diaPago2 = null)
    {
        var usuario = new Usuario { UserName = $"{Guid.NewGuid():N}@t.co", Email = $"{Guid.NewGuid():N}@t.co", Nombre = "Test", DiaPago1 = diaPago1, DiaPago2 = diaPago2 };
        db.Users.Add(usuario);
        await db.SaveChangesAsync();
        return usuario;
    }

    private static async Task<Categoria> CrearCategoriaAsync(ApplicationDbContext db, Usuario usuario, string nombre, TipoCategoria tipo)
    {
        var categoria = new Categoria { UsuarioId = usuario.Id, Nombre = nombre, Tipo = tipo };
        db.Categorias.Add(categoria);
        await db.SaveChangesAsync();
        return categoria;
    }

    private static async Task AgregarGastoAsync(ApplicationDbContext db, Usuario usuario, Categoria categoria, decimal monto, DateTime fecha)
    {
        db.MovimientosDiaADia.Add(new MovimientoDiaADia { UsuarioId = usuario.Id, CategoriaId = categoria.Id, Monto = monto, Fecha = fecha });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task SinNingunDato_NoDaNingunTip()
    {
        await using var db = NuevoContexto();
        var usuario = await CrearUsuarioAsync(db);

        var tip = await InsightsService.CalcularTipAsync(usuario.Id, db, new DateTime(2026, 6, 15));

        Assert.Null(tip);
    }

    [Fact]
    public async Task PresupuestoConSobregastoProyectado_TieneLaMayorPrioridad()
    {
        await using var db = NuevoContexto();
        // DiaPago1 cercano y con harto gasto también sería válido para el
        // tip de "ritmo" — esto confirma que sobregasto de presupuesto le
        // gana aunque ambas condiciones se cumplan a la vez.
        var usuario = await CrearUsuarioAsync(db, diaPago1: 20);
        var categoria = await CrearCategoriaAsync(db, usuario, "Mercado", TipoCategoria.Gasto);
        var hoy = new DateTime(2026, 6, 10);

        db.Presupuestos.Add(new Presupuesto { UsuarioId = usuario.Id, CategoriaId = categoria.Id, MontoLimite = 300_000m, Mes = 6, Anio = 2026 });
        await db.SaveChangesAsync();

        // 200.000 en 10 días proyecta a 600.000 en el mes (30 días) > límite de 300.000.
        await AgregarGastoAsync(db, usuario, categoria, 200_000m, hoy);

        var tip = await InsightsService.CalcularTipAsync(usuario.Id, db, hoy);

        Assert.NotNull(tip);
        Assert.Contains("Mercado", tip);
        Assert.Contains("presupuesto", tip, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SinSobregasto_PeroConPagoCercano_AvisaDelRitmo()
    {
        await using var db = NuevoContexto();
        var usuario = await CrearUsuarioAsync(db, diaPago1: 15);
        var categoria = await CrearCategoriaAsync(db, usuario, "Ocio", TipoCategoria.Gasto);
        // Día 10, próximo pago el 15 -> faltan 5 días, exactamente el borde de la ventana.
        var hoy = new DateTime(2026, 6, 10);

        await AgregarGastoAsync(db, usuario, categoria, 100_000m, hoy);

        var tip = await InsightsService.CalcularTipAsync(usuario.Id, db, hoy);

        Assert.NotNull(tip);
        Assert.Contains("5 días", tip);
    }

    [Fact]
    public async Task SinPresupuestoNiDiaDePago_CaeEnComparacionHistorica()
    {
        await using var db = NuevoContexto();
        var usuario = await CrearUsuarioAsync(db);
        var categoria = await CrearCategoriaAsync(db, usuario, "Mercado", TipoCategoria.Gasto);
        var hoy = new DateTime(2026, 6, 10);

        // Promedio de marzo/abril/mayo: 300.000/mes.
        await AgregarGastoAsync(db, usuario, categoria, 300_000m, new DateTime(2026, 3, 15));
        await AgregarGastoAsync(db, usuario, categoria, 300_000m, new DateTime(2026, 4, 15));
        await AgregarGastoAsync(db, usuario, categoria, 300_000m, new DateTime(2026, 5, 15));
        // Junio va mucho más alto de lo normal.
        await AgregarGastoAsync(db, usuario, categoria, 500_000m, hoy);

        var tip = await InsightsService.CalcularTipAsync(usuario.Id, db, hoy);

        Assert.NotNull(tip);
        Assert.Contains("promedio", tip, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("encima", tip, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IngresosNoCuentanComoGastoEnLaComparacionHistorica()
    {
        await using var db = NuevoContexto();
        var usuario = await CrearUsuarioAsync(db);
        var salario = await CrearCategoriaAsync(db, usuario, "Salario", TipoCategoria.Ingreso);
        var hoy = new DateTime(2026, 6, 10);

        // Solo hay ingresos históricos, ningún gasto — no debería generar
        // ninguna comparación (evita tratar el sueldo como si fuera gasto).
        await AgregarGastoAsync(db, usuario, salario, 3_000_000m, new DateTime(2026, 5, 15));

        var tip = await InsightsService.CalcularTipAsync(usuario.Id, db, hoy);

        Assert.Null(tip);
    }
}
