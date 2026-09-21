using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MoneyBack.Api.Tests;

/// <summary>
/// La API serializa enums como texto ("Gasto", no 0) vía
/// ConfigureHttpJsonOptions en Program.cs. Los helpers de System.Net.Http.Json
/// no heredan esa configuración — usan JsonSerializerOptions.Default, que no
/// sabe leer un enum como string y truena. En vez de pasar unas options
/// explícitas en cada llamada de cada test, estos métodos con el mismo
/// nombre y firma, declarados en este mismo namespace, ganan por resolución
/// de extensiones de C# sobre los de System.Net.Http.Json — así todos los
/// PostAsJsonAsync/GetFromJsonAsync/etc. de los tests quedan arreglados sin
/// tocar cada sitio donde se llaman.
/// </summary>
public static class JsonTestExtensions
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static Task<HttpResponseMessage> PostAsJsonAsync<TValue>(this HttpClient client, string? requestUri, TValue value, CancellationToken ct = default) =>
        HttpClientJsonExtensions.PostAsJsonAsync(client, requestUri, value, Options, ct);

    public static Task<HttpResponseMessage> PutAsJsonAsync<TValue>(this HttpClient client, string? requestUri, TValue value, CancellationToken ct = default) =>
        HttpClientJsonExtensions.PutAsJsonAsync(client, requestUri, value, Options, ct);

    public static Task<TValue?> GetFromJsonAsync<TValue>(this HttpClient client, string? requestUri, CancellationToken ct = default) =>
        HttpClientJsonExtensions.GetFromJsonAsync<TValue>(client, requestUri, Options, ct);

    public static Task<TValue?> ReadFromJsonAsync<TValue>(this HttpContent content, CancellationToken ct = default) =>
        HttpContentJsonExtensions.ReadFromJsonAsync<TValue>(content, Options, ct);
}
