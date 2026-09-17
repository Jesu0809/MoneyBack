using Microsoft.EntityFrameworkCore;
using MoneyBack.Api.Data;
using MoneyBack.Api.Models.DiaADia;
using MoneyBack.Api.Models.Metas;

namespace MoneyBack.Api.Services;

public static class RedondeoService
{
    private const decimal UnidadRedondeo = 1000m;

    /// <summary>
    /// Si el usuario pertenece a un hogar con el redondeo activado, calcula
    /// el "vuelto" del gasto hasta el siguiente $1.000 y lo reparte como
    /// aportes automáticos entre las metas activas del hogar, según sus
    /// porcentajes configurados. No hace nada si el usuario no tiene hogar,
    /// el redondeo está apagado, o el gasto ya cae en un múltiplo exacto.
    /// </summary>
    public static async Task AplicarSiCorrespondeAsync(MovimientoDiaADia gasto, ApplicationDbContext db)
    {
        var hogar = await db.Hogares
            .FirstOrDefaultAsync(h => h.Usuario1Id == gasto.UsuarioId || h.Usuario2Id == gasto.UsuarioId);

        if (hogar is null || !hogar.RedondeoActivo) return;

        var redondeado = Math.Ceiling(gasto.Monto / UnidadRedondeo) * UnidadRedondeo;
        var vuelto = redondeado - gasto.Monto;
        if (vuelto <= 0) return;

        var metasActivas = await db.MetasAhorro
            .Where(m => m.HogarId == hogar.Id && m.Activa &&
                        (m.Tipo == TipoMeta.Apartamento || m.Tipo == TipoMeta.Emergencia))
            .ToListAsync();

        var metaApartamento = metasActivas.FirstOrDefault(m => m.Tipo == TipoMeta.Apartamento);
        var metaEmergencia = metasActivas.FirstOrDefault(m => m.Tipo == TipoMeta.Emergencia);

        if (metaApartamento is not null && hogar.PorcentajeRedondeoApartamento > 0)
        {
            db.MovimientosMeta.Add(NuevoAporte(metaApartamento.Id, gasto, vuelto * hogar.PorcentajeRedondeoApartamento / 100m));
        }

        if (metaEmergencia is not null && hogar.PorcentajeRedondeoEmergencia > 0)
        {
            db.MovimientosMeta.Add(NuevoAporte(metaEmergencia.Id, gasto, vuelto * hogar.PorcentajeRedondeoEmergencia / 100m));
        }

        gasto.RedondeoAplicado = true;
    }

    private static MovimientoMeta NuevoAporte(int metaId, MovimientoDiaADia gasto, decimal monto) => new()
    {
        MetaAhorroId = metaId,
        UsuarioId = gasto.UsuarioId,
        Tipo = TipoMovimiento.Aporte,
        Monto = Math.Round(monto, 0, MidpointRounding.AwayFromZero),
        Nota = "Redondeo automático de gasto",
        EsAutomatico = true
    };
}
