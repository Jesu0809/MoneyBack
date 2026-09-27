namespace MoneyBack.Web.Services;

/// <param name="Titulo">Qué va a pasar, en una frase.</param>
/// <param name="Detalle">Lo que se pierde. Vacío si no se pierde nada.</param>
/// <param name="TextoConfirmar">El verbo, no "Aceptar": el botón tiene que decir qué hace.</param>
/// <param name="EsDestructiva">Pinta el botón en rojo y le quita el foco visual.</param>
public record Confirmacion(
    string Titulo,
    string? Detalle = null,
    string TextoConfirmar = "Confirmar",
    bool EsDestructiva = true);

/// <summary>
/// Preguntar "¿seguro?" antes de algo que no se puede deshacer.
///
/// Hacía falta porque había cuatro botones de borrar —un movimiento del día
/// a día, un cobro fijo, una deuda, una tarjeta— que borraban al primer
/// toque, sin preguntar y sin forma de recuperar. En el día a día el botón
/// de borrar mide 30 píxeles y está pegado al de editar: un roce con el
/// pulgar y el gasto desaparece.
///
/// Vive como servicio y no como un componente por pantalla porque la hoja
/// se dibuja en el layout, por fuera de `.page-enter` — ahí adentro queda
/// atrapada bajo la barra de arriba y la de abajo por más z-index que
/// tenga.
/// </summary>
public class ConfirmacionService
{
    public event Action? OnCambio;

    public Confirmacion? Pendiente { get; private set; }

    private TaskCompletionSource<bool>? _espera;

    public Task<bool> PreguntarAsync(Confirmacion confirmacion)
    {
        // Si ya había una abierta, se cancela: dos hojas encimadas no se
        // pueden leer y la de abajo quedaría esperando para siempre.
        _espera?.TrySetResult(false);

        Pendiente = confirmacion;
        _espera = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        OnCambio?.Invoke();
        return _espera.Task;
    }

    public void Responder(bool confirmado)
    {
        Pendiente = null;
        var espera = _espera;
        _espera = null;
        OnCambio?.Invoke();
        espera?.TrySetResult(confirmado);
    }
}
