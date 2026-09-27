using Microsoft.EntityFrameworkCore;
using MoneyBack.Api.Models;
using MoneyBack.Api.Models.DiaADia;
using MoneyBack.Api.Models.Auth;
using MoneyBack.Api.Models.Deudas;
using MoneyBack.Api.Models.Metas;
using MoneyBack.Api.Models.Tarjetas;

namespace MoneyBack.Api.Data;

/// <summary>
/// Le pone techo a los campos de texto que escribe la gente.
///
/// En Postgres un string sin longitud es `text`, que no tiene límite: se
/// podían guardar cinco mil caracteres como nombre de una categoría, y el
/// API los aceptaba con un 201. No es solo desorden — una nota de diez mil
/// caracteres viaja en cada listado de movimientos, se muestra en las
/// notificaciones y revienta el diseño de cualquier pantalla donde aparezca.
///
/// Los topes son generosos a propósito: lo suficiente para cualquier uso
/// real y lo bastante bajo para que nada de esto pase.
/// </summary>
public static class LongitudesDeTexto
{
    private const int Nombre = 80;
    private const int Nota = 300;
    private const int Icono = 16;

    public static void Aplicar(ModelBuilder builder)
    {
        builder.Entity<Usuario>().Property(u => u.Nombre).HasMaxLength(Nombre);

        builder.Entity<Categoria>(e =>
        {
            e.Property(c => c.Nombre).HasMaxLength(Nombre);
            e.Property(c => c.Icono).HasMaxLength(Icono);
        });

        builder.Entity<MovimientoDiaADia>().Property(m => m.Nota).HasMaxLength(Nota);

        builder.Entity<MetaAhorro>(e =>
        {
            e.Property(m => m.Nombre).HasMaxLength(Nombre);
            e.Property(m => m.Icono).HasMaxLength(Icono);
        });

        builder.Entity<MovimientoMeta>().Property(m => m.Nota).HasMaxLength(Nota);
        builder.Entity<Deuda>().Property(d => d.Nombre).HasMaxLength(Nombre);
        builder.Entity<TarjetaCredito>().Property(t => t.Nombre).HasMaxLength(Nombre);
        builder.Entity<TokenAtajo>().Property(t => t.Nombre).HasMaxLength(Nombre);
    }
}
