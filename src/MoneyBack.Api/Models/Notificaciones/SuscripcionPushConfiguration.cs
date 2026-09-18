using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MoneyBack.Api.Models.Notificaciones;

public class SuscripcionPushConfiguration : IEntityTypeConfiguration<SuscripcionPush>
{
    public void Configure(EntityTypeBuilder<SuscripcionPush> builder)
    {
        // El endpoint que entrega pushManager.subscribe() ya es único por
        // navegador/dispositivo — evita duplicar la fila si el mismo
        // navegador vuelve a llamar /suscribirse (el endpoint hace upsert).
        builder.HasIndex(p => p.Endpoint).IsUnique();
    }
}
