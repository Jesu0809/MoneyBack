using System.Text.RegularExpressions;
using MoneyBack.Api.Services;

namespace MoneyBack.Api.Tests;

/// <summary>
/// El bug que estas pruebas existen para evitar ya ocurrió: el resumen
/// semanal llevaba desde que se escribió fallando en su primera consulta,
/// cada domingo, sin enviar uno solo. La causa era una fecha con
/// Kind=Unspecified contra una columna "timestamp with time zone".
///
/// EF InMemory no valida el Kind y Postgres sí, así que ninguna prueba de
/// las que había podía verlo. Estas miran el Kind directamente.
/// </summary>
public class HoraColombiaTests
{
    [Fact]
    public void LoQueVaALaBaseSiempreSaleEnUtc()
    {
        var hoy = HoraColombia.Hoy();

        Assert.Equal(DateTimeKind.Utc, HoraColombia.AInstanteUtc(hoy).Kind);
        Assert.Equal(DateTimeKind.Utc, HoraColombia.InicioDelDiaUtc(hoy).Kind);
        Assert.Equal(DateTimeKind.Utc, HoraColombia.InicioDelMesUtc(hoy).Kind);
    }

    /// <summary>
    /// Hoy() es un punto del calendario, no un instante. Que salga
    /// Unspecified es la señal de que no debe ir a la base sin convertirse.
    /// </summary>
    [Fact]
    public void HoyNoEsUnInstanteYPorEsoNoSirveParaLaBase()
    {
        Assert.Equal(DateTimeKind.Unspecified, HoraColombia.Hoy().Kind);
    }

    /// <summary>
    /// Colombia no tiene horario de verano: son -5 todo el año. Si esto
    /// cambiara, los cortes de mes y de semana se correrían una hora.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(12)]
    public void ColombiaEsSiempreCincoHorasDetrasDeUtc(int mes)
    {
        var medianoche = new DateTime(2026, mes, 15, 0, 0, 0);

        var enUtc = HoraColombia.AInstanteUtc(medianoche);

        Assert.Equal(5, (enUtc - medianoche).TotalHours);
    }

    /// <summary>
    /// El caso que importa: a las 8 de la noche del 31 en Colombia, en UTC
    /// ya es día 1 del mes siguiente. Si el corte se calculara con la fecha
    /// del servidor, el gasto del último día del mes contaría contra el mes
    /// entrante.
    /// </summary>
    [Fact]
    public void ElCorteDeMesSigueElCalendarioColombianoYNoElDelServidor()
    {
        var ultimoDiaEnLaNoche = new DateTime(2026, 1, 31, 20, 0, 0);

        var inicioDeMes = HoraColombia.InicioDelMesUtc(ultimoDiaEnLaNoche);

        // 1 de enero a medianoche en Bogotá = 1 de enero 05:00 UTC.
        Assert.Equal(new DateTime(2026, 1, 1, 5, 0, 0, DateTimeKind.Utc), inicioDeMes);
    }

    [Fact]
    public void ElInicioDelDiaEsLaMedianocheDeAcaNoLaDeUtc()
    {
        var unaTarde = new DateTime(2026, 6, 10, 15, 30, 0);

        Assert.Equal(new DateTime(2026, 6, 10, 5, 0, 0, DateTimeKind.Utc),
            HoraColombia.InicioDelDiaUtc(unaTarde));
    }

    /// <summary>
    /// Un linter para este error concreto. Construir un DateTime a mano
    /// dentro de un servicio casi siempre significa "una fecha del
    /// calendario de acá", y si eso llega a una consulta sin pasar por
    /// HoraColombia, Postgres la rechaza y el servicio entero se cae. No
    /// falla al compilar y ninguna prueba con InMemory lo ve.
    /// </summary>
    [Fact]
    public void NingunServicioArmaFechasASuManera()
    {
        var servicios = Directory.EnumerateFiles(CarpetaDeServicios(), "*.cs")
            .Where(a => Path.GetFileName(a) != "HoraColombia.cs");

        var sospechosos = new List<string>();
        foreach (var archivo in servicios)
        {
            var texto = File.ReadAllText(archivo);

            // Convertir desde UTC a mano: para eso está HoraColombia.Hoy().
            if (texto.Contains("ConvertTimeFromUtc") || texto.Contains("ConvertTimeToUtc"))
            {
                sospechosos.Add($"{Path.GetFileName(archivo)}: convierte zonas por su cuenta");
            }

            // new DateTime(...) sin Kind explícito queda en Unspecified.
            foreach (Match m in Regex.Matches(texto, @"new DateTime\([^)]*\)"))
            {
                if (!m.Value.Contains("DateTimeKind"))
                {
                    sospechosos.Add($"{Path.GetFileName(archivo)}: {m.Value}");
                }
            }
        }

        Assert.Empty(sospechosos);
    }

    private static string CarpetaDeServicios()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "MoneyBack.Api")))
        {
            dir = dir.Parent;
        }
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "src", "MoneyBack.Api", "Services");
    }
}
