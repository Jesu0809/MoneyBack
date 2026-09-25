using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MoneyBack.Api.Models.DiaADia;

public class ComercioCategoriaConfiguration : IEntityTypeConfiguration<ComercioCategoria>
{
    public void Configure(EntityTypeBuilder<ComercioCategoria> builder)
    {
        // Un comercio solo puede apuntar a una categoría por persona: si
        // pudiera haber dos filas para "oxxo calle 100", el gasto entraría en
        // una u otra según el orden de la consulta, y sería imposible
        // explicarle a nadie por qué cambió de categoría sola.
        builder.HasIndex(c => new { c.UsuarioId, c.Comercio }).IsUnique();

        builder.Property(c => c.Comercio).HasMaxLength(60);

        // Si se borra la categoría, se borra lo aprendido: dejar el mapeo
        // apuntando a una categoría que ya no existe rompería el registro
        // automático sin que nadie entienda por qué.
        builder.HasOne(c => c.Categoria)
            .WithMany()
            .HasForeignKey(c => c.CategoriaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
