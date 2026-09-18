using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MoneyBack.Api.Models.Deudas;

public class DeudaConfiguration : IEntityTypeConfiguration<Deuda>
{
    public void Configure(EntityTypeBuilder<Deuda> builder)
    {
        // Respaldo a nivel de base de datos de la regla "exactamente un
        // dueño": ni un futuro endpoint ni un script de datos puede dejar
        // una Deuda sin UsuarioId ni HogarId, o con ambos a la vez.
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_Deuda_UnSoloPropietario",
            "(\"UsuarioId\" IS NOT NULL) <> (\"HogarId\" IS NOT NULL)"));
    }
}
