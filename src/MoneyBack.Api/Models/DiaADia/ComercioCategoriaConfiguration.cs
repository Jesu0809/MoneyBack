using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MoneyBack.Api.Models.DiaADia;

public class MovimientoDiaADiaConfiguration : IEntityTypeConfiguration<MovimientoDiaADia>
{
    public void Configure(EntityTypeBuilder<MovimientoDiaADia> builder)
    {
        // Mismo largo que la llave aprendida: si acá cupiera más, un comercio
        // de nombre largo se guardaría completo en el movimiento pero recortado
        // en lo aprendido, y dejarían de coincidir entre sí.
        builder.Property(m => m.Comercio).HasMaxLength(60);

        // Esta es la tabla que crece para siempre: cada gasto, todos los días,
        // y ahora sin que nadie los escriba porque los mete el atajo. Casi
        // todas las consultas de la app son "los movimientos de esta persona
        // entre estas dos fechas", y con índices sueltos por columna Postgres
        // tiene que traer todos los del usuario y filtrar por fecha después.
        // Con el índice compuesto lee solo el rango que necesita.
        builder.HasIndex(m => new { m.UsuarioId, m.Fecha });
    }
}

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
