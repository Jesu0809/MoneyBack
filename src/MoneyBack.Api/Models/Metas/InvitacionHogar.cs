using MoneyBack.Api.Models;

namespace MoneyBack.Api.Models.Metas;

/// <summary>
/// Antes, escribir el correo de tu pareja creaba el Hogar de una, sin que
/// ella confirmara nada. Ahora eso solo crea esta invitación — el Hogar
/// real (ver HogaresEndpoints) solo nace cuando el invitado la acepta.
/// Guarda la configuración inicial (tope VIS, reparto de redondeo) para
/// aplicarla al Hogar en ese momento, sin tener que volver a pedirla.
/// </summary>
public class InvitacionHogar
{
    public int Id { get; set; }

    /// <summary>
    /// El grupo al que se invita. Antes la invitación no apuntaba a nada:
    /// aceptarla CREABA un hogar entre dos personas, así que no se podía
    /// invitar a alguien a un grupo que ya existía, ni sumar a una tercera.
    /// </summary>
    public int HogarId { get; set; }
    public Hogar Hogar { get; set; } = null!;

    public int InvitadorId { get; set; }
    public Usuario Invitador { get; set; } = null!;

    public int InvitadoId { get; set; }
    public Usuario Invitado { get; set; } = null!;

    public EstadoInvitacionHogar Estado { get; set; } = EstadoInvitacionHogar.Pendiente;


    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaResolucion { get; set; }
}
