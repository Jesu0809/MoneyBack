using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MoneyBack.Api.Models.Notificaciones;

public class NotificacionConfiguration : IEntityTypeConfiguration<Notificacion>
{
    public void Configure(EntityTypeBuilder<Notificacion> builder)
    {
        builder.Property(n => n.Titulo).HasMaxLength(120).IsRequired();
        builder.Property(n => n.Cuerpo).HasMaxLength(600).IsRequired();
        builder.Property(n => n.Url).HasMaxLength(200);

        // El panel siempre pide lo de una persona, lo más reciente primero, y
        // el contador de la campanita filtra por no leídas. Sin este índice
        // cada apertura recorre la tabla entera, que solo crece.
        builder.HasIndex(n => new { n.UsuarioId, n.CreadaEn });
        builder.HasIndex(n => new { n.UsuarioId, n.LeidaEn });
    }
}
