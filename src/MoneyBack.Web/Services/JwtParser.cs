using System.Security.Claims;
using System.Text.Json;

namespace MoneyBack.Web.Services;

/// <summary>
/// Decodifica el payload de un JWT a claims sin validar la firma — la
/// validación real ya la hizo el API al emitirlo; aquí solo leemos lo
/// que ya confiamos para poblar el ClaimsPrincipal del lado del cliente.
/// </summary>
public static class JwtParser
{
    public static IEnumerable<Claim> ParseClaims(string jwt)
    {
        var payload = jwt.Split('.')[1];
        var json = Convert.FromBase64String(PadBase64Url(payload));
        using var doc = JsonDocument.Parse(json);

        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in prop.Value.EnumerateArray())
                {
                    yield return new Claim(prop.Name, item.ToString());
                }
            }
            else
            {
                yield return new Claim(prop.Name, prop.Value.ToString());
            }
        }
    }

    private static string PadBase64Url(string base64Url)
    {
        var s = base64Url.Replace('-', '+').Replace('_', '/');
        return s.PadRight(s.Length + (4 - s.Length % 4) % 4, '=');
    }
}
