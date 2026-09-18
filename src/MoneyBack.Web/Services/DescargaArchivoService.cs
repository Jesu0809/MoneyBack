using Microsoft.JSInterop;

namespace MoneyBack.Web.Services;

/// <summary>
/// Wrapper de wwwroot/js/interop.js (primer módulo JS del proyecto) para
/// descargar archivos (Excel/PDF de reportes) que llegan como bytes ya
/// autenticados desde ApiClient.
/// </summary>
public class DescargaArchivoService(IJSRuntime js)
{
    private IJSObjectReference? _modulo;

    private async Task<IJSObjectReference> ObtenerModuloAsync()
    {
        _modulo ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/interop.js");
        return _modulo;
    }

    public async Task DescargarAsync(byte[] bytes, string nombreArchivo, string tipoMime)
    {
        var modulo = await ObtenerModuloAsync();
        await modulo.InvokeVoidAsync("downloadFile", bytes, nombreArchivo, tipoMime);
    }
}
