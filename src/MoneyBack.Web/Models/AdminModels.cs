namespace MoneyBack.Web.Models;

public record UsuarioAdminResponse(int Id, string Nombre, string Email, DateTime FechaCreacion, List<string> Roles);

/// <param name="Clave">Se muestra una sola vez: el servidor solo guarda su hash.</param>
public record ClaveTemporalResponse(string Clave, int SesionesCerradas);

public record RotarCodigoInvitacionRequest(string NuevoCodigo);

public record CodigoInvitacionInfo(int Id, DateTime CreadoEn);

/// <param name="UltimaActividad">Lo más reciente que hizo: un gasto, un aporte o entrar.</param>
public record CuentaAdminResponse(
    int Id, string Nombre, string Email, DateTime FechaCreacion, List<string> Roles,
    int SesionesActivas, int Movimientos, int Grupos, DateTime? UltimaActividad, bool EsMiCuenta)
{
    public bool EsAdministrador => Roles.Contains("SuperAdmin");
}

public record ResumenAdminResponse(
    int Cuentas, int Administradores, int SesionesActivas,
    int Movimientos, int MovimientosSinClasificar,
    int Grupos, int MetasActivas,
    DateTime? UltimaLlamadaAtajo, bool UltimaLlamadaAtajoFueBien, int LlamadasAtajoFallidas,
    int NotificacionesSinLeer);

public record QueSeBorrariaResponse(
    string Nombre, string Email,
    int Movimientos, int Categorias, int Aportes, int Deudas, int Tarjetas,
    int CobrosFijos, int Presupuestos, int Notificaciones,
    List<string> GruposQueSeBorran, List<string> GruposDeLosQueSale,
    string? Bloqueo);
