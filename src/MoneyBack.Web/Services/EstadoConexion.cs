namespace MoneyBack.Web.Services;

public enum Conexion
{
    /// <summary>Hablando con el servidor y sin nada pendiente.</summary>
    EnLinea,

    /// <summary>Sin servidor, pero anotando en el teléfono.</summary>
    Registrando,

    /// <summary>Sin servidor y sin nada esperando.</summary>
    SinConexion
}

/// <summary>
/// El estado de la conexión, para el punto que va junto al nombre de la app.
///
/// Es la idea del LED de un router: uno no lee un mensaje, mira el color de
/// reojo y sabe. Importa porque la app ahora sigue funcionando sin señal —
/// anota los gastos y los sube después— y sin una señal visible no habría
/// forma de notar la diferencia entre "quedó guardado allá" y "quedó
/// guardado acá esperando".
///
/// Se actualiza sola desde el manejador de red, no hay que llamarla en cada
/// pantalla.
/// </summary>
public class EstadoConexion
{
    private bool _enLinea = true;
    private int _pendientes;

    public event Action? OnCambio;

    public int PendientesPorSubir => _pendientes;

    public Conexion Actual => _enLinea
        ? Conexion.EnLinea
        : _pendientes > 0 ? Conexion.Registrando : Conexion.SinConexion;

    public string Descripcion => Actual switch
    {
        Conexion.EnLinea => "Todo al día",
        Conexion.Registrando => $"Sin conexión · {_pendientes} por subir",
        _ => "Sin conexión"
    };

    public void MarcarEnLinea()
    {
        if (_enLinea) return;
        _enLinea = true;
        OnCambio?.Invoke();
    }

    public void MarcarSinConexion()
    {
        if (!_enLinea) return;
        _enLinea = false;
        OnCambio?.Invoke();
    }

    public void ActualizarPendientes(int cuantos)
    {
        if (_pendientes == cuantos) return;
        _pendientes = cuantos;
        OnCambio?.Invoke();
    }
}
