using Microsoft.EntityFrameworkCore;
using MoneyBack.Api.Data;
using MoneyBack.Api.Models.DiaADia;
using MoneyBack.Api.Models.Metas;

namespace MoneyBack.Api.Services;

public static class RedondeoService
{
    private const decimal UnidadRedondeo = 1000m;

    /// <summary>
    /// Si la persona pertenece a un grupo con el vuelto activado, calcula
    /// cuánto falta para el siguiente $1.000 y lo reparte entre las metas
    /// activas, según el porcentaje que cada una tenga asignado.
    ///
    /// El reparto vive en cada meta y no en el grupo. Antes eran dos campos
    /// fijos —uno para apartamento, otro para emergencia— y eso hacía
    /// literalmente imposible tener una tercera meta.
    /// </summary>
    public static async Task AplicarSiCorrespondeAsync(MovimientoDiaADia gasto, ApplicationDbContext db)
    {
        // El vuelto va a los grupos donde la persona tenga el redondeo
        // activo. Con varios grupos ya no hay "el hogar" de alguien, y
        // quedarse con el primero haría que el ahorro dependiera del orden
        // en que se crearon — invisible y arbitrario.
        var hogares = await db.Hogares
            .Where(h => h.RedondeoActivo && h.Miembros.Any(m => m.UsuarioId == gasto.UsuarioId))
            .Select(h => h.Id)
            .ToListAsync();

        if (hogares.Count == 0) return;

        var redondeado = Math.Ceiling(gasto.Monto / UnidadRedondeo) * UnidadRedondeo;
        var vuelto = redondeado - gasto.Monto;
        if (vuelto <= 0) return;

        var metas = await db.MetasAhorro
            .Where(m => hogares.Contains(m.HogarId) && m.Activa && m.PorcentajeRedondeo > 0)
            .ToListAsync();

        if (metas.Count == 0) return;

        // Se normaliza sobre lo que realmente suman las metas activas, no
        // sobre 100. Si alguien archiva la meta que tenía el 80%, el vuelto
        // no debe perderse en el camino: se reparte entre las que quedan.
        var totalPorcentajes = metas.Sum(m => m.PorcentajeRedondeo);
        if (totalPorcentajes <= 0) return;

        foreach (var meta in metas)
        {
            var parte = vuelto * meta.PorcentajeRedondeo / totalPorcentajes;
            var aporte = Math.Round(parte, 0, MidpointRounding.AwayFromZero);
            if (aporte > 0) db.MovimientosMeta.Add(NuevoAporte(meta.Id, gasto, aporte));
        }

        gasto.RedondeoAplicado = true;
    }

    private static MovimientoMeta NuevoAporte(int metaId, MovimientoDiaADia gasto, decimal monto) => new()
    {
        MetaAhorroId = metaId,
        UsuarioId = gasto.UsuarioId,
        Tipo = TipoMovimiento.Aporte,
        Monto = monto,
        Nota = "Redondeo automático de gasto",
        EsAutomatico = true
    };
}
