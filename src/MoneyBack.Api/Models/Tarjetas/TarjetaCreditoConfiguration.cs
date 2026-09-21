using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoneyBack.Api.Models.DiaADia;

namespace MoneyBack.Api.Models.Tarjetas;

public class TarjetaCreditoConfiguration : IEntityTypeConfiguration<TarjetaCredito>
{
    public void Configure(EntityTypeBuilder<TarjetaCredito> builder)
    {
        // Fijado a propósito, aunque coincide con lo que EF Core ya haría
        // por convención para una FK nullable: si esto alguna vez cambiara
        // a Cascade sin querer (por ejemplo al endurecer la relación a
        // requerida), borrar una tarjeta borraría los gastos etiquetados
        // con ella — y eso resucitaría el bug de doble conteo que esta
        // función existe para evitar, ahora al revés (restando el saldo
        // acumulado retroactivamente al borrar la tarjeta). SetNull
        // explícito documenta la garantía: la tarjeta se puede borrar
        // libremente, el historial de gastos nunca se toca.
        builder.HasMany<MovimientoDiaADia>()
            .WithOne(m => m.TarjetaCredito)
            .HasForeignKey(m => m.TarjetaCreditoId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
