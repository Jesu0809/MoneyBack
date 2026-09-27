using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MoneyBack.Api.Models.Metas;

/// <summary>
/// Acá vivía la configuración de las dos FKs a Usuario (Usuario1 y
/// Usuario2), que EF exigía porque eran dos caminos de borrado hacia la
/// misma tabla. Al pasar los miembros a su propia tabla ese problema
/// desapareció: ahora hay una sola relación, y está en
/// MiembroHogarConfiguration.
/// </summary>
public class HogarConfiguration : IEntityTypeConfiguration<Hogar>
{
    public void Configure(EntityTypeBuilder<Hogar> builder)
    {
        builder.Property(h => h.Nombre).HasMaxLength(60);
    }
}
