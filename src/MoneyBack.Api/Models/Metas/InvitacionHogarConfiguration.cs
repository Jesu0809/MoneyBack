using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MoneyBack.Api.Models.Metas;

/// <summary>
/// Igual que HogarConfiguration: dos relaciones hacia Usuario (Invitador e
/// Invitado) necesitan configuración explícita o EF Core no sabe resolver
/// el borrado en cascada por dos caminos hacia la misma tabla.
/// </summary>
public class InvitacionHogarConfiguration : IEntityTypeConfiguration<InvitacionHogar>
{
    public void Configure(EntityTypeBuilder<InvitacionHogar> builder)
    {
        builder.HasOne(i => i.Invitador)
            .WithMany()
            .HasForeignKey(i => i.InvitadorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Invitado)
            .WithMany()
            .HasForeignKey(i => i.InvitadoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
