using MoneyBack.Web.Services;
using MoneyBack.Web.Tests.Infra;

namespace MoneyBack.Web.Tests;

/// <summary>
/// A qué mes pertenece un gasto.
///
/// El caso que lo destapó: el 30 de septiembre a las 8:15 p.m. en Bogotá la
/// app abrió mostrando "octubre 2026". En UTC ya era 1 de octubre a la 1:15,
/// y el mes se estaba calculando con UtcNow. El gasto quedó guardado bien
/// —el instante es correcto— pero contado contra el mes equivocado.
/// </summary>
public class MesDelCalendarioTests
{
    /// <summary>
    /// Bogotá, 30 de septiembre, 8:15 p.m. En UTC eso ya es octubre.
    /// </summary>
    private static readonly DateTimeOffset LaNocheDelCaso =
        new(2026, 9, 30, 20, 15, 0, TimeSpan.FromHours(-5));

    [Fact]
    public void ElInstanteDeEseGastoEnUtcEsDeOctubre()
    {
        // Confirma la premisa: no es que la app inventara octubre.
        Assert.Equal(10, LaNocheDelCaso.UtcDateTime.Month);
        Assert.Equal(1, LaNocheDelCaso.UtcDateTime.Day);
    }

    /// <summary>
    /// El inicio del mes de acá no es medianoche UTC: son las 5 de la
    /// mañana UTC. Esas cinco horas son justo las que se contaban mal.
    /// </summary>
    [Fact]
    public void ElMesDeAcaEmpiezaCincoHorasDespuesQueElDeUtc()
    {
        var septiembre = new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Local);

        var inicioUtc = HoraLocal.InicioDeMesUtc(septiembre);

        Assert.Equal(DateTimeKind.Utc, inicioUtc.Kind);
        // Con el teléfono en Bogotá: 1 de septiembre 00:00 local = 05:00 UTC.
        Assert.Equal(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Local).ToUniversalTime(), inicioUtc);
    }

    /// <summary>
    /// El gasto de las 8:15 p.m. del 30 tiene que caer DENTRO del rango de
    /// septiembre. Con los límites en UTC caía fuera, en octubre.
    /// </summary>
    [Fact]
    public void ElGastoDeLaNocheDelUltimoDiaCuentaEnSuMes()
    {
        var septiembre = new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Local);

        var desde = HoraLocal.InicioDeMesUtc(septiembre);
        var hasta = HoraLocal.FinDeMesUtc(septiembre);
        var elGasto = LaNocheDelCaso.UtcDateTime;

        Assert.InRange(elGasto, desde, hasta);
    }

    [Fact]
    public void YNoSeCuelaEnElMesSiguiente()
    {
        var octubre = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Local);

        var desde = HoraLocal.InicioDeMesUtc(octubre);
        var elGasto = LaNocheDelCaso.UtcDateTime;

        Assert.True(elGasto < desde, "El gasto del 30 de septiembre se está contando en octubre.");
    }

    /// <summary>
    /// El último instante del mes se calcula como "inicio del siguiente
    /// menos un tic" para no tener que pensar en meses de 28, 30 o 31.
    /// </summary>
    [Theory]
    [InlineData(2, 2026)]  // 28 días
    [InlineData(2, 2024)]  // bisiesto, 29
    [InlineData(4, 2026)]  // 30
    [InlineData(12, 2026)] // 31, y cambia de año
    public void ElFinDeMesNoDependeDeCuantosDiasTengaElMes(int mes, int anio)
    {
        var elMes = new DateTime(anio, mes, 15, 12, 0, 0, DateTimeKind.Local);

        var fin = HoraLocal.FinDeMesUtc(elMes);
        var inicioDelSiguiente = HoraLocal.InicioDeMesUtc(elMes.AddMonths(1));

        Assert.Equal(inicioDelSiguiente.AddTicks(-1), fin);
    }

    [Fact]
    public void LosMesesSeguidosNoDejanHuecoNiSeSolapan()
    {
        var septiembre = new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Local);

        var finDeSeptiembre = HoraLocal.FinDeMesUtc(septiembre);
        var inicioDeOctubre = HoraLocal.InicioDeMesUtc(septiembre.AddMonths(1));

        // Un tic exacto entre uno y otro: ni un instante en los dos, ni uno
        // que se quede sin mes.
        Assert.Equal(1, (inicioDeOctubre - finDeSeptiembre).Ticks);
    }

    [Fact]
    public void LoQueSeMandaAlApiVaSiempreEnUtc()
    {
        var local = new DateTime(2026, 9, 30, 20, 15, 0, DateTimeKind.Local);

        Assert.Equal(DateTimeKind.Utc, HoraLocal.AInstanteUtc(local).Kind);
        Assert.Equal(DateTimeKind.Utc, HoraLocal.InicioDeMesUtc(local).Kind);
        Assert.Equal(DateTimeKind.Utc, HoraLocal.FinDeMesUtc(local).Kind);
    }

    /// <summary>
    /// Un linter como el del servidor: ninguna pantalla puede volver a
    /// decidir un mes o un día con UtcNow. El instante de un movimiento sí
    /// es UTC —eso está bien— pero el calendario no.
    /// </summary>
    [Fact]
    public void NingunaPantallaDecideElCalendarioConUtcNow()
    {
        var raiz = UbicarWeb();
        var sospechosos = new List<string>();

        foreach (var archivo in Directory.EnumerateFiles(Path.Combine(raiz, "Pages"), "*.razor"))
        {
            foreach (var linea in File.ReadAllLines(archivo))
            {
                if (!linea.Contains("DateTime.UtcNow")) continue;

                // Restar dos instantes para saber "hace cuánto" es correcto.
                if (linea.Contains("DateTime.UtcNow -")) continue;
                // Guardar el instante de un movimiento también.
                if (linea.Contains("_form.Monto")) continue;

                sospechosos.Add($"{Path.GetFileName(archivo)}: {linea.Trim()}");
            }
        }

        Assert.Empty(sospechosos);
    }

    private static string UbicarWeb()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "MoneyBack.Web")))
        {
            dir = dir.Parent;
        }
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "src", "MoneyBack.Web");
    }
}
