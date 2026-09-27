using MoneyBack.Web.Models;

namespace MoneyBack.Web.Services;

/// <summary>
/// Un gasto anotado sin conexión, esperando turno para subir.
/// </summary>
/// <param name="ClaveLocal">
/// Identifica el gasto mientras no tiene Id del servidor, para poder
/// mostrarlo en la lista y quitarlo cuando suba.
/// </param>
public record MovimientoPendiente(
    string ClaveLocal,
    int CategoriaId,
    string CategoriaNombre,
    string CategoriaIcono,
    TipoCategoria Tipo,
    decimal Monto,
    DateTime Fecha,
    string? Nota,
    int? TarjetaCreditoId);

/// <summary>
/// Guarda los gastos que no se pudieron subir y los sube cuando vuelve la
/// conexión.
///
/// Sin esto, anotar un gasto en el ascensor o en un parqueadero sin señal
/// terminaba en un error y el gasto se perdía — y quien pierde un par así
/// deja de confiar en los totales y vuelve al papel. Anotar tiene que
/// funcionar siempre; subir puede esperar.
///
/// La cola vive en el teléfono, así que sobrevive a cerrar la app.
/// </summary>
public class ColaPendientes(AlmacenLocal almacen, ApiClient api, EstadoConexion estado)
{
    private const string Clave = "moneyback.pendientes";

    public event Action? OnCambio;

    public async Task<List<MovimientoPendiente>> ListarAsync() =>
        await almacen.LeerAsync<List<MovimientoPendiente>>(Clave) ?? [];

    public async Task AgregarAsync(MovimientoPendiente pendiente)
    {
        var cola = await ListarAsync();
        cola.Add(pendiente);
        await almacen.GuardarAsync(Clave, cola);
        estado.ActualizarPendientes(cola.Count);
        OnCambio?.Invoke();
    }

    /// <summary>
    /// Intenta subir todo lo que espera. Devuelve cuántos lograron subir.
    ///
    /// Se para en el primer fallo de red en vez de seguir: si no hay
    /// conexión, insistir con los demás solo gasta batería. Pero un gasto
    /// que el servidor RECHAZA —una categoría borrada, por ejemplo— se
    /// descarta, porque reintentarlo eternamente dejaría la cola trancada
    /// para siempre.
    /// </summary>
    public async Task<int> SubirPendientesAsync()
    {
        var cola = await ListarAsync();
        if (cola.Count == 0) return 0;

        var subidos = 0;
        var quedan = new List<MovimientoPendiente>();

        foreach (var pendiente in cola)
        {
            if (quedan.Count > 0)
            {
                // Ya falló uno por red: no tiene sentido seguir intentando.
                quedan.Add(pendiente);
                continue;
            }

            var resultado = await api.RegistrarMovimientoDiaADiaAsync(new CrearMovimientoDiaADiaRequest(
                pendiente.CategoriaId, pendiente.Monto, pendiente.Fecha, pendiente.Nota, pendiente.TarjetaCreditoId));

            if (resultado.Exito)
            {
                subidos++;
            }
            else if (resultado.EsFalloDeRed)
            {
                quedan.Add(pendiente);
            }
            else
            {
                // El servidor lo rechazó por el contenido. Reintentarlo
                // siempre trancaría la cola; se descarta y se sigue.
                subidos++;
            }
        }

        if (quedan.Count == 0) await almacen.BorrarAsync(Clave);
        else await almacen.GuardarAsync(Clave, quedan);

        estado.ActualizarPendientes(quedan.Count);

        if (subidos > 0) OnCambio?.Invoke();
        return subidos;
    }
}
