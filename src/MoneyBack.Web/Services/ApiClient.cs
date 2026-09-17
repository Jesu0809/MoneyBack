using System.Net.Http.Json;
using MoneyBack.Web.Models;

namespace MoneyBack.Web.Services;

public class ApiClient(IHttpClientFactory httpClientFactory)
{
    private HttpClient Api => httpClientFactory.CreateClient("Api");

    public async Task<PerfilResponse?> ObtenerPerfilAsync()
    {
        var response = await Api.GetAsync("api/auth/me");
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<PerfilResponse>() : null;
    }

    public async Task<HogarResponse?> ObtenerMiHogarAsync()
    {
        var response = await Api.GetAsync("api/hogares/mio");
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<HogarResponse>() : null;
    }

    public async Task<ApiResult<HogarResponse>> CrearHogarAsync(CrearHogarRequest request)
    {
        var response = await Api.PostAsJsonAsync("api/hogares", request);
        return await ApiResult<HogarResponse>.FromResponseAsync(response, r => r.Content.ReadFromJsonAsync<HogarResponse>()!);
    }

    public async Task<ApiResult<HogarResponse>> ActualizarHogarAsync(ActualizarHogarRequest request)
    {
        var response = await Api.PutAsJsonAsync("api/hogares/mio", request);
        return await ApiResult<HogarResponse>.FromResponseAsync(response, r => r.Content.ReadFromJsonAsync<HogarResponse>()!);
    }

    public async Task<List<MetaResponse>> ObtenerMetasAsync(int hogarId)
    {
        var response = await Api.GetAsync($"api/hogares/{hogarId}/metas");
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<MetaResponse>>() ?? [];
    }

    public async Task<ApiResult<MetaResponse>> CrearMetaAsync(int hogarId, CrearMetaRequest request)
    {
        var response = await Api.PostAsJsonAsync($"api/hogares/{hogarId}/metas", request);
        return await ApiResult<MetaResponse>.FromResponseAsync(response, r => r.Content.ReadFromJsonAsync<MetaResponse>()!);
    }

    public async Task<MetaDetalleResponse?> ObtenerMetaAsync(int metaId)
    {
        var response = await Api.GetAsync($"api/metas/{metaId}");
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<MetaDetalleResponse>() : null;
    }

    public async Task<ApiResult<int>> RegistrarMovimientoAsync(int metaId, CrearMovimientoRequest request)
    {
        var response = await Api.PostAsJsonAsync($"api/metas/{metaId}/movimientos", request);
        return await ApiResult<int>.FromResponseAsync(response, r => r.Content.ReadFromJsonAsync<int>());
    }

    public async Task<bool> ArchivarMetaAsync(int metaId)
    {
        var response = await Api.PostAsync($"api/metas/{metaId}/archivar", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<ApiResult<SimulacionSubsidiosResponse>> SimularSubsidiosAsync(int hogarId, SimularSubsidiosRequest request)
    {
        var response = await Api.PostAsJsonAsync($"api/hogares/{hogarId}/subsidios/simular", request);
        return await ApiResult<SimulacionSubsidiosResponse>.FromResponseAsync(response, r => r.Content.ReadFromJsonAsync<SimulacionSubsidiosResponse>()!);
    }

    public async Task<List<UsuarioAdminResponse>> ObtenerUsuariosAdminAsync()
    {
        var response = await Api.GetAsync("api/admin/usuarios");
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<UsuarioAdminResponse>>() ?? [];
    }

    public async Task<int> RevocarSesionesAsync(int usuarioId)
    {
        var response = await Api.PostAsync($"api/admin/usuarios/{usuarioId}/revocar-sesiones", null);
        if (!response.IsSuccessStatusCode) return 0;
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, int>>();
        return result?.GetValueOrDefault("sesionesRevocadas") ?? 0;
    }

    public async Task<ApiResult<object?>> RotarCodigoInvitacionAsync(RotarCodigoInvitacionRequest request)
    {
        var response = await Api.PostAsJsonAsync("api/admin/codigo-invitacion/rotar", request);
        return await ApiResult<object?>.FromResponseAsync(response, _ => Task.FromResult<object?>(null));
    }
}
