using MoneyBack.Api.Models.Subsidios;

namespace MoneyBack.Api.Dtos;

public record SimularSubsidiosRequest(
    decimal IngresoCombinadoMensual,
    decimal ValorVivienda,
    bool EsVip,
    /// <summary>
    /// Monto del subsidio de caja de compensación (Colsubsidio u otra). No se
    /// calcula: cada caja publica su propia tabla y cambia con frecuencia, así
    /// que el usuario lo ingresa manualmente tras consultar su portal transaccional.
    /// </summary>
    decimal MontoSubsidioCajaCompensacion = 0);

public record AlertaMetaApartamento(
    int MetaId,
    string Nombre,
    decimal MontoObjetivo,
    bool SuperaTopeVis);

public record SimulacionSubsidiosResponse(
    decimal SmmlvVigente,
    TipoTopeVis TipoTopeVis,
    decimal TopeVisPesos,
    decimal ValorVivienda,
    bool EsVis,
    decimal IngresoCombinadoMensual,
    decimal IngresoCombinadoEnSmmlv,
    /// <summary>
    /// Mi Casa Ya se descontinuó (sin presupuesto para 2026); su reemplazo,
    /// Mi Casa Milagro, todavía no publica reglas ni montos oficiales — así
    /// que en vez de inventar una cifra, esto es solo un mensaje de estado.
    /// </summary>
    string NotaProgramaGobierno,
    decimal MontoSubsidioCajaCompensacion,
    decimal TotalSubsidiosEstimado,
    AlertaMetaApartamento? MetaApartamento);

/// <param name="AfiliadoCajaCompensacion">
/// Si alguno de los dos cotiza a una caja. Se pregunta porque hoy es la ayuda
/// más grande a la que se puede aspirar de verdad —Mi Casa Ya quedó casi sin
/// cupos— y cambia el resultado en decenas de millones.
/// </param>
/// <param name="ViveEnBogota">
/// Bogotá tiene su propio subsidio que se suma a los demás. Se pregunta
/// aparte del hogar porque el tope VIS cubre cinco ciudades, pero el subsidio
/// distrital del que hay cifras verificadas es solo el de Bogotá.
/// </param>
/// <param name="Cesantias">
/// Se pueden retirar para comprar vivienda, así que para la cuota inicial
/// cuentan igual que el ahorro. Para muchos hogares SON la cuota inicial, y
/// dejarlas fuera hacía ver inalcanzable algo que ya tenían reunido.
/// </param>
public record PlanViviendaRequest(
    decimal ValorVivienda,
    decimal IngresoCombinadoMensual,
    bool AfiliadoCajaCompensacion,
    bool ViveEnBogota = false,
    decimal Cesantias = 0,
    /// <summary>
    /// En zonas de renovación urbana el tope VIS sube de 150 a 175 SMMLV.
    /// Importa en Bogotá, donde buena parte de los proyectos nuevos están en
    /// esas zonas: son casi cuarenta millones más de margen para seguir
    /// siendo VIS, es decir, para no quedarse sin subsidios.
    /// </summary>
    bool EsRenovacionUrbana = false,
    /// <summary>
    /// Año en que se firma la escritura. El tope VIS se mide en esa fecha,
    /// no hoy, así que un proyecto sobre planos que entrega en 2029 se
    /// compara contra el tope de 2029. Sin esto, la app descartaba como "No
    /// VIS" proyectos que sí califican, que es justo lo que pasa con los
    /// proyectos nuevos del norte de Bogotá.
    /// </summary>
    int? AnioEscrituracion = null);
