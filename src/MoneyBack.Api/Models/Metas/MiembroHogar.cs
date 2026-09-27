using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoneyBack.Api.Models;

namespace MoneyBack.Api.Models.Metas;

/// <summary>
/// Una persona dentro de un grupo de ahorro.
///
/// Antes el grupo tenía Usuario1Id y Usuario2Id: exactamente dos personas,
/// ni una más. Eso alcanzaba para una pareja y para nada más — ahorrar en
/// familia, con hermanos o con amigos era imposible por la forma de la
/// tabla, no por una decisión de producto.
/// </summary>
public class MiembroHogar
{
    public int Id { get; set; }

    public int HogarId { get; set; }
    public Hogar Hogar { get; set; } = null!;

    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public DateTime FechaIngreso { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Quien creó el grupo. Puede invitar y renombrar; los demás solo
    /// aportan y ven. No es jerarquía por gusto: sin alguien responsable,
    /// cualquiera podría sacar a cualquiera de un grupo con la plata de
    /// todos adentro.
    /// </summary>
    public bool EsAdministrador { get; set; }
}

public class MiembroHogarConfiguration : IEntityTypeConfiguration<MiembroHogar>
{
    public void Configure(EntityTypeBuilder<MiembroHogar> builder)
    {
        // Nadie dos veces en el mismo grupo.
        builder.HasIndex(m => new { m.HogarId, m.UsuarioId }).IsUnique();

        builder.HasOne(m => m.Usuario)
            .WithMany()
            .HasForeignKey(m => m.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Hogar)
            .WithMany(h => h.Miembros)
            .HasForeignKey(m => m.HogarId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
