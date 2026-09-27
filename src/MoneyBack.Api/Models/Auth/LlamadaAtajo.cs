using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoneyBack.Api.Models;

namespace MoneyBack.Api.Models.Auth;

/// <summary>
/// Deja constancia de cada vez que un atajo llama al servidor, haya
/// funcionado o no.
///
/// Existe porque "el atajo no hizo nada" tenía dos explicaciones —la
/// automatización no se disparó, o sí llamó y falló algo acá— y ninguna forma
/// de distinguirlas. iOS no muestra un historial de automatizaciones, así que
/// la única evidencia posible es la de este lado. Sin esto solo quedaba
/// adivinar, y adivinar sobre la plata de alguien no sirve.
///
/// Se guardan las últimas <see cref="MaximoPorUsuario"/> por cuenta: alcanza
/// para revisar qué pasó ayer y evita que la tabla crezca sin control.
/// </summary>
public class LlamadaAtajo
{
    /// <summary>
    /// Suficiente para reconstruir un día de compras. Más allá de eso, un
    /// registro viejo de lo que dice un SMS del banco es información sensible
    /// guardada sin que le sirva a nadie.
    /// </summary>
    public const int MaximoPorUsuario = 30;

    public int Id { get; set; }

    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public DateTime Fecha { get; set; } = DateTime.UtcNow;

    /// <summary>El texto que mandó el atajo, recortado.</summary>
    public string? Texto { get; set; }

    public bool Exito { get; set; }

    /// <summary>Qué se registró, o por qué no se pudo.</summary>
    public string? Detalle { get; set; }
}

public class LlamadaAtajoConfiguration : IEntityTypeConfiguration<LlamadaAtajo>
{
    public void Configure(EntityTypeBuilder<LlamadaAtajo> builder)
    {
        builder.Property(l => l.Texto).HasMaxLength(200);
        builder.Property(l => l.Detalle).HasMaxLength(200);

        // Siempre se consulta "las últimas de esta persona".
        builder.HasIndex(l => new { l.UsuarioId, l.Fecha });
    }
}
