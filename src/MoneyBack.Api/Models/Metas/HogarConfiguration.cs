using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MoneyBack.Api.Models.Metas;

/// <summary>
/// Configuración explícita necesaria porque Hogar tiene DOS relaciones
/// hacia Usuario (Usuario1 y Usuario2). Sin esto, EF Core no sabe cómo
/// resolver el borrado en cascada por dos caminos distintos hacia la
/// misma tabla y falla al crear la migración.
/// </summary>
public class HogarConfiguration : IEntityTypeConfiguration<Hogar>
{
    public void Configure(EntityTypeBuilder<Hogar> builder)
    {
        builder.HasOne(h => h.Usuario1)
            .WithMany()
            .HasForeignKey(h => h.Usuario1Id)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(h => h.Usuario2)
            .WithMany()
            .HasForeignKey(h => h.Usuario2Id)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
