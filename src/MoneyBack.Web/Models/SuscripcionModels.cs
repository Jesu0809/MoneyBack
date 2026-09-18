using System.Text.Json.Serialization;

namespace MoneyBack.Web.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FrecuenciaSuscripcion { Semanal, Mensual, Anual }

public record CrearSuscripcionRequest(
    string Nombre, decimal Monto, int CategoriaId, FrecuenciaSuscripcion Frecuencia,
    DateTime ProximoCobro, int DiasAvisoPrevio);

public record ActualizarSuscripcionRequest(
    string Nombre, decimal Monto, int CategoriaId, FrecuenciaSuscripcion Frecuencia,
    int DiasAvisoPrevio, bool Activa);

public record SuscripcionResponse(
    int Id, string Nombre, decimal Monto, int CategoriaId, string CategoriaNombre, string CategoriaIcono,
    FrecuenciaSuscripcion Frecuencia, DateTime ProximoCobro, int DiasAvisoPrevio, bool Activa);

public record ConfirmacionPendienteResponse(
    int Id, int SuscripcionId, string SuscripcionNombre, decimal Monto, string CategoriaIcono, DateTime PeriodoCobro);

public record ResolverConfirmacionRequest(bool Ocurrio);
