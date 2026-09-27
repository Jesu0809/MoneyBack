using MoneyBack.Web.Models;

namespace MoneyBack.Web.Tests.Infra;

/// <summary>Respuestas de ejemplo, para que cada prueba solo escriba lo que le importa.</summary>
public static class Datos
{
    public static HogarResponse Grupo(int id = 1, string nombre = "Grupo de prueba", params string[] miembros) => new(
        id, nombre, AplicaTope150: true, RedondeoActivo: true, SoyAdministrador: true,
        (miembros.Length == 0 ? ["Ana"] : miembros)
            .Select((n, i) => new MiembroResponse(i + 1, n, i == 0)).ToList());

    public static MetaResponse Meta(
        int id = 10, string nombre = "Cuota inicial", string icono = "🏠",
        decimal objetivo = 50_000_000, decimal actual = 5_000_000,
        bool activa = true, decimal porcentajeRedondeo = 100, int hogarId = 1) => new(
        id, hogarId, nombre, icono, EsFondoEmergencia: false, porcentajeRedondeo,
        objetivo, actual, objetivo == 0 ? 0 : Math.Round(actual / objetivo * 100, 2),
        null, activa, DateTime.UtcNow);

    public static MetaDetalleResponse Detalle(MetaResponse m, params AportePorUsuario[] aportes) => new(
        m.Id, m.HogarId, m.Nombre, m.Icono, m.EsFondoEmergencia, m.PorcentajeRedondeo,
        m.MontoObjetivo, m.MontoActual, m.PorcentajeCompletado, m.FechaObjetivoEstimada,
        m.Activa, m.FechaCreacion, aportes.ToList(), []);

    public static CategoriaResponse Categoria(int id = 100, string nombre = "Mercado", string icono = "🛒",
        TipoCategoria tipo = TipoCategoria.Gasto, bool activa = true) => new(id, nombre, tipo, icono, activa);

    public static MisInvitacionesHogarResponse SinInvitaciones => new(null, null);
}
