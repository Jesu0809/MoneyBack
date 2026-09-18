using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MoneyBack.Api.Models.Suscripciones;

public class ConfirmacionCobroConfiguration : IEntityTypeConfiguration<ConfirmacionCobro>
{
    public void Configure(EntityTypeBuilder<ConfirmacionCobro> builder)
    {
        // Evita duplicados si RevisionSuscripcionesService llega a correr dos
        // veces para el mismo período (por ejemplo si algún día hay más de
        // una instancia del API corriendo a la vez).
        builder.HasIndex(c => new { c.SuscripcionId, c.PeriodoCobro }).IsUnique();
    }
}
