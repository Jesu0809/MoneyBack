using System.Net.Http.Json;
using MoneyBack.Web.Models;

namespace MoneyBack.Web.Services;

public class ApiResult<T>
{
    public bool Exito { get; private init; }
    public T? Valor { get; private init; }
    public string? MensajeError { get; private init; }
    public Dictionary<string, string[]>? ErroresPorCampo { get; private init; }

    public static async Task<ApiResult<T>> FromResponseAsync(HttpResponseMessage response, Func<HttpResponseMessage, Task<T>> leerExito)
    {
        if (response.IsSuccessStatusCode)
        {
            return new ApiResult<T> { Exito = true, Valor = await leerExito(response) };
        }

        string? mensaje = null;
        Dictionary<string, string[]>? errores = null;

        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>();
            mensaje = problem?.Title;
            errores = problem?.Errors;
        }
        catch
        {
            mensaje = await SafeReadStringAsync(response);
        }

        mensaje ??= response.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized => "Correo o contraseña incorrectos.",
            System.Net.HttpStatusCode.Forbidden => "No tienes permiso para hacer esto.",
            System.Net.HttpStatusCode.NotFound => "No se encontró lo que buscabas.",
            System.Net.HttpStatusCode.Conflict => "Ya existe un conflicto con esta operación.",
            _ => "Ocurrió un error inesperado. Intenta de nuevo."
        };

        return new ApiResult<T> { Exito = false, MensajeError = mensaje, ErroresPorCampo = errores };
    }

    private static async Task<string?> SafeReadStringAsync(HttpResponseMessage response)
    {
        try { return await response.Content.ReadAsStringAsync(); }
        catch { return null; }
    }

    public string PrimerError()
    {
        if (ErroresPorCampo is { Count: > 0 })
        {
            return ErroresPorCampo.First().Value.FirstOrDefault() ?? MensajeError ?? "Error desconocido.";
        }
        return MensajeError ?? "Error desconocido.";
    }
}
