namespace MoneyBack.Api.Models.Deudas;

/// <summary>
/// Explícito en vez de inferirlo de cuál FK (UsuarioId/HogarId) está llena
/// — más claro de leer en queries y en la UI, aunque el CHECK constraint de
/// DeudaConfiguration sigue siendo la garantía real a nivel de base de datos.
/// </summary>
public enum TipoPropiedadDeuda
{
    Privada,
    Compartida
}
