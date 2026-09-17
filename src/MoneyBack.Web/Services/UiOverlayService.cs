namespace MoneyBack.Web.Services;

/// <summary>
/// Le permite a una página avisarle a MainLayout que hay un modal de
/// pantalla completa abierto, para que oculte el tabbar mientras tanto
/// (evita que el tabbar quede pintado encima del modal).
/// </summary>
public class UiOverlayService
{
    public bool ModalPantallaCompletaAbierto { get; private set; }

    public event Action? OnChange;

    public void AbrirModal()
    {
        ModalPantallaCompletaAbierto = true;
        OnChange?.Invoke();
    }

    public void CerrarModal()
    {
        ModalPantallaCompletaAbierto = false;
        OnChange?.Invoke();
    }
}
