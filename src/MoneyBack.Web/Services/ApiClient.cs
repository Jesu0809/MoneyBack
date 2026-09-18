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

    public async Task<ApiResult<PerfilResponse>> ActualizarPerfilAsync(ActualizarPerfilRequest request)
    {
        var response = await Api.PutAsJsonAsync("api/auth/me", request);
        return await ApiResult<PerfilResponse>.FromResponseAsync(response, r => r.Content.ReadFromJsonAsync<PerfilResponse>()!);
    }

    public async Task<ApiResult<object?>> CambiarPasswordAsync(CambiarPasswordRequest request)
    {
        var response = await Api.PostAsJsonAsync("api/auth/cambiar-password", request);
        return await ApiResult<object?>.FromResponseAsync(response, _ => Task.FromResult<object?>(null));
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

    public async Task<List<CategoriaResponse>> ObtenerCategoriasAsync()
    {
        var response = await Api.GetAsync("api/categorias");
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<CategoriaResponse>>() ?? [];
    }

    public async Task<ApiResult<CategoriaResponse>> CrearCategoriaAsync(CrearCategoriaRequest request)
    {
        var response = await Api.PostAsJsonAsync("api/categorias", request);
        return await ApiResult<CategoriaResponse>.FromResponseAsync(response, r => r.Content.ReadFromJsonAsync<CategoriaResponse>()!);
    }

    public async Task<ApiResult<CategoriaResponse>> ActualizarCategoriaAsync(int id, ActualizarCategoriaRequest request)
    {
        var response = await Api.PutAsJsonAsync($"api/categorias/{id}", request);
        return await ApiResult<CategoriaResponse>.FromResponseAsync(response, r => r.Content.ReadFromJsonAsync<CategoriaResponse>()!);
    }

    public async Task<bool> EliminarCategoriaAsync(int id)
    {
        var response = await Api.DeleteAsync($"api/categorias/{id}");
        return response.IsSuccessStatusCode;
    }

    public async Task<int> SembrarCategoriasBasicasAsync()
    {
        var response = await Api.PostAsync("api/categorias/sembrar-basicas", null);
        if (!response.IsSuccessStatusCode) return 0;
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, int>>();
        return result?.GetValueOrDefault("agregadas") ?? 0;
    }

    public async Task<List<MovimientoDiaADiaResponse>> ObtenerMovimientosDiaADiaAsync(DateTime? desde = null, DateTime? hasta = null)
    {
        var query = ConstruirQueryFechas(desde, hasta);
        var response = await Api.GetAsync($"api/movimientos-diaadia{query}");
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<MovimientoDiaADiaResponse>>() ?? [];
    }

    public async Task<ResumenDiaADiaResponse?> ObtenerResumenDiaADiaAsync(DateTime? desde = null, DateTime? hasta = null)
    {
        var query = ConstruirQueryFechas(desde, hasta);
        var response = await Api.GetAsync($"api/movimientos-diaadia/resumen{query}");
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<ResumenDiaADiaResponse>() : null;
    }

    public async Task<ApiResult<MovimientoDiaADiaResponse>> RegistrarMovimientoDiaADiaAsync(CrearMovimientoDiaADiaRequest request)
    {
        var response = await Api.PostAsJsonAsync("api/movimientos-diaadia", request);
        return await ApiResult<MovimientoDiaADiaResponse>.FromResponseAsync(response, r => r.Content.ReadFromJsonAsync<MovimientoDiaADiaResponse>()!);
    }

    public async Task<bool> EliminarMovimientoDiaADiaAsync(int id)
    {
        var response = await Api.DeleteAsync($"api/movimientos-diaadia/{id}");
        return response.IsSuccessStatusCode;
    }

    public async Task<List<PresupuestoResponse>> ObtenerPresupuestosAsync(int mes, int anio)
    {
        var response = await Api.GetAsync($"api/presupuestos?mes={mes}&anio={anio}");
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<PresupuestoResponse>>() ?? [];
    }

    public async Task<ApiResult<object?>> GuardarPresupuestoAsync(GuardarPresupuestoRequest request)
    {
        var response = await Api.PostAsJsonAsync("api/presupuestos", request);
        return await ApiResult<object?>.FromResponseAsync(response, _ => Task.FromResult<object?>(null));
    }

    public async Task<bool> EliminarPresupuestoAsync(int id)
    {
        var response = await Api.DeleteAsync($"api/presupuestos/{id}");
        return response.IsSuccessStatusCode;
    }

    public async Task<List<SuscripcionResponse>> ObtenerSuscripcionesAsync()
    {
        var response = await Api.GetAsync("api/suscripciones");
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<SuscripcionResponse>>() ?? [];
    }

    public async Task<List<ConfirmacionPendienteResponse>> ObtenerConfirmacionesPendientesAsync()
    {
        var response = await Api.GetAsync("api/suscripciones/confirmaciones-pendientes");
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<ConfirmacionPendienteResponse>>() ?? [];
    }

    public async Task<ApiResult<SuscripcionResponse>> CrearSuscripcionAsync(CrearSuscripcionRequest request)
    {
        var response = await Api.PostAsJsonAsync("api/suscripciones", request);
        return await ApiResult<SuscripcionResponse>.FromResponseAsync(response, r => r.Content.ReadFromJsonAsync<SuscripcionResponse>()!);
    }

    public async Task<ApiResult<SuscripcionResponse>> ActualizarSuscripcionAsync(int id, ActualizarSuscripcionRequest request)
    {
        var response = await Api.PutAsJsonAsync($"api/suscripciones/{id}", request);
        return await ApiResult<SuscripcionResponse>.FromResponseAsync(response, r => r.Content.ReadFromJsonAsync<SuscripcionResponse>()!);
    }

    public async Task<bool> EliminarSuscripcionAsync(int id)
    {
        var response = await Api.DeleteAsync($"api/suscripciones/{id}");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ResolverConfirmacionAsync(int suscripcionId, int confirmacionId, bool ocurrio)
    {
        var response = await Api.PostAsJsonAsync(
            $"api/suscripciones/{suscripcionId}/confirmaciones/{confirmacionId}/resolver",
            new ResolverConfirmacionRequest(ocurrio));
        return response.IsSuccessStatusCode;
    }

    public async Task<List<DeudaResponse>> ObtenerDeudasAsync()
    {
        var response = await Api.GetAsync("api/deudas");
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<DeudaResponse>>() ?? [];
    }

    public async Task<ApiResult<DeudaResponse>> CrearDeudaPrivadaAsync(CrearDeudaPrivadaRequest request)
    {
        var response = await Api.PostAsJsonAsync("api/deudas/privada", request);
        return await ApiResult<DeudaResponse>.FromResponseAsync(response, r => r.Content.ReadFromJsonAsync<DeudaResponse>()!);
    }

    public async Task<ApiResult<DeudaResponse>> CrearDeudaCompartidaAsync(int hogarId, CrearDeudaCompartidaRequest request)
    {
        var response = await Api.PostAsJsonAsync($"api/hogares/{hogarId}/deudas", request);
        return await ApiResult<DeudaResponse>.FromResponseAsync(response, r => r.Content.ReadFromJsonAsync<DeudaResponse>()!);
    }

    public async Task<ApiResult<DeudaResponse>> PagarCuotaAsync(int deudaId, PagarCuotaRequest request)
    {
        var response = await Api.PostAsJsonAsync($"api/deudas/{deudaId}/pagar-cuota", request);
        return await ApiResult<DeudaResponse>.FromResponseAsync(response, r => r.Content.ReadFromJsonAsync<DeudaResponse>()!);
    }

    public async Task<bool> EliminarDeudaAsync(int id)
    {
        var response = await Api.DeleteAsync($"api/deudas/{id}");
        return response.IsSuccessStatusCode;
    }

    private static string ConstruirQueryFechas(DateTime? desde, DateTime? hasta)
    {
        var partes = new List<string>();
        if (desde is not null) partes.Add($"desde={Uri.EscapeDataString(desde.Value.ToString("o"))}");
        if (hasta is not null) partes.Add($"hasta={Uri.EscapeDataString(hasta.Value.ToString("o"))}");
        return partes.Count > 0 ? "?" + string.Join("&", partes) : "";
    }
}
