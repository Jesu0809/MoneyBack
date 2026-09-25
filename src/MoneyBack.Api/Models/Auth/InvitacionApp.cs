using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoneyBack.Api.Models;

namespace MoneyBack.Api.Models.Auth;

/// <summary>
/// Un enlace personal para que alguien pueda registrarse en MoneyBack.
///
/// Existe aparte del CodigoInvitacion global porque son dos cosas distintas:
/// aquel es la llave de toda la app, y compartirlo significa darle a alguien
/// la misma llave que a todos los demás, sin forma de saber quién entró por
/// quién ni de revocarle el acceso a uno solo. Esto es una invitación
/// concreta: la hace una persona, sirve una vez, y se sabe a quién dejó
/// entrar.
///
/// Igual que TokenAtajo y RefreshToken, nunca se guarda en claro: solo su
/// hash. El enlace se muestra una vez, al crearlo.
/// </summary>
public class InvitacionApp
{
    public int Id { get; set; }

    public int CreadoPorUsuarioId { get; set; }
    public Usuario CreadoPor { get; set; } = null!;

    public string TokenHash { get; set; } = string.Empty;

    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Un enlace sin vencimiento tirado en un chat es una puerta abierta que
    /// nadie recuerda que existe. Una semana alcanza de sobra para que la
    /// persona a quien se lo mandaste lo abra.
    /// </summary>
    public DateTime ExpiraEn { get; set; } = DateTime.UtcNow.AddDays(7);

    /// <summary>
    /// Se marca con la hora antes de saber quién se registró, para reservarla:
    /// es lo que impide que dos personas usen el mismo enlace al tiempo.
    /// </summary>
    public DateTime? UsadaEn { get; set; }

    public int? UsadaPorUsuarioId { get; set; }
    public Usuario? UsadaPor { get; set; }

    public bool Anulada { get; set; }

    public bool Disponible(DateTime ahora) => !Anulada && UsadaEn is null && ExpiraEn > ahora;
}

public class InvitacionAppConfiguration : IEntityTypeConfiguration<InvitacionApp>
{
    public void Configure(EntityTypeBuilder<InvitacionApp> builder)
    {
        builder.HasIndex(i => i.TokenHash).IsUnique();

        // Marcar quién usó la invitación es una carrera: dos personas pueden
        // abrir el mismo enlace al tiempo, leer que está libre y registrarse
        // las dos. Como token de concurrencia, la reserva viaja en el WHERE
        // del UPDATE ("...y UsadaEn sigue siendo NULL"), así que la segunda
        // falla en vez de pisar a la primera.
        builder.Property(i => i.UsadaEn).IsConcurrencyToken();

        // Dos FKs al mismo tipo: EF no sabe cuál navegación va con cuál y hay
        // que decírselo. Sin esto, la migración inventa columnas sombra.
        builder.HasOne(i => i.CreadoPor)
            .WithMany()
            .HasForeignKey(i => i.CreadoPorUsuarioId)
            .OnDelete(DeleteBehavior.Cascade);

        // Quien entró por el enlace no se lleva la invitación si borra su
        // cuenta: el registro de que alguien la usó sigue siendo cierto.
        builder.HasOne(i => i.UsadaPor)
            .WithMany()
            .HasForeignKey(i => i.UsadaPorUsuarioId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
