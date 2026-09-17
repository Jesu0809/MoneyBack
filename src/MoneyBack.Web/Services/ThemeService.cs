using Microsoft.JSInterop;

namespace MoneyBack.Web.Services;

public enum Tema { Sistema, Claro, Oscuro }

/// <summary>
/// El CSS ya sigue prefers-color-scheme automáticamente; esto añade la
/// opción de forzar claro/oscuro por encima de eso, vía el atributo
/// data-theme en <html>, persistido en localStorage.
/// </summary>
public class ThemeService(IJSRuntime js)
{
    private const string StorageKey = "moneyback.tema";

    public Tema Actual { get; private set; } = Tema.Sistema;

    public event Action? OnChange;

    public async Task InicializarAsync()
    {
        var guardado = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        Actual = guardado switch
        {
            "claro" => Tema.Claro,
            "oscuro" => Tema.Oscuro,
            _ => Tema.Sistema
        };
        await AplicarAsync();
    }

    public async Task CambiarAsync(Tema tema)
    {
        Actual = tema;
        var valor = tema switch { Tema.Claro => "claro", Tema.Oscuro => "oscuro", _ => "sistema" };
        await js.InvokeVoidAsync("localStorage.setItem", StorageKey, valor);
        await AplicarAsync();
        OnChange?.Invoke();
    }

    private async Task AplicarAsync()
    {
        var atributo = Actual switch
        {
            Tema.Claro => "light",
            Tema.Oscuro => "dark",
            _ => null as string
        };

        if (atributo is null)
        {
            await js.InvokeVoidAsync("document.documentElement.removeAttribute", "data-theme");
        }
        else
        {
            await js.InvokeVoidAsync("document.documentElement.setAttribute", "data-theme", atributo);
        }
    }
}
