using System.Text.Json;
using Microsoft.JSInterop;

namespace MoneyBack.Web.Services;

/// <summary>
/// Guarda cosas en el teléfono, en JSON.
///
/// Toda lectura y escritura va envuelta: en modo privado, con el almacenamiento
/// bloqueado o si el navegador se queda sin espacio, localStorage lanza. Que
/// eso tumbe la pantalla sería absurdo — lo que hay acá es una comodidad, no
/// la fuente de verdad.
/// </summary>
public class AlmacenLocal(IJSRuntime js)
{
    private static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web);

    public async Task<T?> LeerAsync<T>(string clave)
    {
        try
        {
            var json = await js.InvokeAsync<string?>("localStorage.getItem", clave);
            return string.IsNullOrEmpty(json) ? default : JsonSerializer.Deserialize<T>(json, Opciones);
        }
        catch
        {
            return default;
        }
    }

    public async Task GuardarAsync<T>(string clave, T valor)
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", clave, JsonSerializer.Serialize(valor, Opciones));
        }
        catch
        {
            // Sin espacio o sin permiso. La app sigue funcionando en línea.
        }
    }

    public async Task BorrarAsync(string clave)
    {
        try { await js.InvokeVoidAsync("localStorage.removeItem", clave); }
        catch { }
    }
}
