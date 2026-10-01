namespace MoneyBack.Api.Dtos;

public record RegistrarUsuarioRequest(string Nombre, string Email, string Password, string CodigoInvitacion);

public record LoginRequest(string Email, string Password);

public record RefrescarTokenRequest(string RefreshToken);

public record AuthResponse(string AccessToken, string RefreshToken, DateTime AccessTokenExpiraEn);

public record PerfilResponse(int Id, string Nombre, string Email, IReadOnlyList<string> Roles, int? DiaPago1, int? DiaPago2);

public record ActualizarPerfilRequest(string Nombre);

public record ActualizarDiasPagoRequest(int? DiaPago1, int? DiaPago2);

public record CambiarPasswordRequest(string PasswordActual, string PasswordNueva);

public record RotarCodigoInvitacionRequest(string NuevoCodigo);

public record UsuarioAdminResponse(int Id, string Nombre, string Email, DateTime FechaCreacion, IReadOnlyList<string> Roles);

/// <param name="Clave">En claro una sola vez: después solo queda el hash.</param>
public record ClaveTemporalResponse(string Clave, int SesionesCerradas);

public record InvitacionAppResponse(
    int Id, string Estado, DateTime CreadoEn, DateTime ExpiraEn, string? UsadaPorNombre);

/// <param name="Token">
/// En claro y una sola vez: de acá en adelante solo queda su hash, así que ni
/// el servidor puede volver a armar el enlace.
/// </param>
public record InvitacionAppCreadaResponse(int Id, string Token, DateTime ExpiraEn);

/// <param name="Codigos">
/// En claro y una sola vez. Después solo queda el hash, así que ni el
/// servidor puede volver a mostrarlos: o se guardan ahora, o hay que generar
/// una lista nueva.
/// </param>
public record CodigosRecuperacionResponse(List<string> Codigos);

public record CodigosRestantesResponse(int Quedan);

public record RecuperarCuentaRequest(string Email, string Codigo, string NuevaPassword);

public record RecuperacionExitosaResponse(int CodigosQueQuedan);

// --- Panel de administración ---

/// <param name="UltimaActividad">Lo más reciente que hizo: un gasto, un aporte o entrar.</param>
public record CuentaAdminResponse(
    int Id, string Nombre, string Email, DateTime FechaCreacion, IReadOnlyList<string> Roles,
    int SesionesActivas, int Movimientos, int Grupos, DateTime? UltimaActividad, bool EsMiCuenta);

public record ResumenAdminResponse(
    int Cuentas, int Administradores, int SesionesActivas,
    int Movimientos, int MovimientosSinClasificar,
    int Grupos, int MetasActivas,
    DateTime? UltimaLlamadaAtajo, bool UltimaLlamadaAtajoFueBien, int LlamadasAtajoFallidas,
    int NotificacionesSinLeer);

/// <param name="Bloqueo">Por qué no se puede borrar, o null si sí se puede.</param>
public record QueSeBorrariaResponse(
    string Nombre, string Email,
    int Movimientos, int Categorias, int Aportes, int Deudas, int Tarjetas,
    int CobrosFijos, int Presupuestos, int Notificaciones,
    IReadOnlyList<string> GruposQueSeBorran,
    IReadOnlyList<string> GruposDeLosQueSale,
    string? Bloqueo);
