using MoneyBack.Api.Models;

namespace MoneyBack.Api.Models.DiaADia;

/// <summary>
/// Deja constancia de que ya se avisó que una categoría cruzó cierto
/// porcentaje de su tope, este mes.
///
/// Existe solo para no repetir el aviso. Un tope se cruza una vez pero se
/// sigue gastando después, así que sin este registro la persona recibiría una
/// notificación por cada compra a partir de ahí — y quien recibe cinco avisos
/// del mismo tope en una tarde silencia la app y no vuelve a enterarse de
/// nada. Vale más avisar una vez y que se lea.
/// </summary>
public class AvisoPresupuesto
{
    public int Id { get; set; }

    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public int CategoriaId { get; set; }
    public Categoria Categoria { get; set; } = null!;

    public int Mes { get; set; }
    public int Anio { get; set; }

    /// <summary>80 o 100. Se guarda el número y no un enum para que agregar
    /// un umbral nuevo no obligue a una migración.</summary>
    public int Umbral { get; set; }

    public DateTime AvisadoEn { get; set; } = DateTime.UtcNow;
}

public class AvisoPresupuestoConfiguration : Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<AvisoPresupuesto>
{
    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<AvisoPresupuesto> builder)
    {
        // La garantía de "una sola vez" es esta llave, no el código que la
        // consulta: dos gastos registrados al tiempo pueden leer que todavía
        // no se ha avisado y decidir ambos avisar. Con el índice, el segundo
        // insert falla y se descarta el aviso repetido.
        builder.HasIndex(a => new { a.UsuarioId, a.CategoriaId, a.Mes, a.Anio, a.Umbral }).IsUnique();

        builder.HasOne(a => a.Categoria)
            .WithMany()
            .HasForeignKey(a => a.CategoriaId)
            .OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Cascade);
    }
}
