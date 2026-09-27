using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoneyBack.Api.Models;

namespace MoneyBack.Api.Models.Auth;

/// <summary>
/// Una clave de un solo uso para recuperar la cuenta cuando se olvida la
/// contraseña.
///
/// La alternativa habitual —mandar un enlace por correo— exige un servicio de
/// envío, un dominio verificado y que el correo no caiga en spam. Para una
/// app de dos personas eso es mucha infraestructura para algo que pasa una
/// vez cada tanto. Con estos códigos la persona se recupera sola, sin
/// depender de un administrador ni de que llegue un correo.
///
/// Se guardan solo como hash, igual que las contraseñas: quien se meta a la
/// base no puede usarlos para entrar.
/// </summary>
public class CodigoRecuperacion
{
    /// <summary>
    /// Suficientes para varios olvidos sin volverse una lista que nadie
    /// guarda. Al usar el último, la app avisa que hay que generar más.
    /// </summary>
    public const int CuantosGenerar = 8;

    public int Id { get; set; }

    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public string CodigoHash { get; set; } = string.Empty;

    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Cuándo se gastó. Un código sirve una sola vez: si sirviera siempre,
    /// una foto vieja de la lista seguiría abriendo la cuenta para siempre.
    /// </summary>
    public DateTime? UsadoEn { get; set; }
}

public class CodigoRecuperacionConfiguration : IEntityTypeConfiguration<CodigoRecuperacion>
{
    public void Configure(EntityTypeBuilder<CodigoRecuperacion> builder)
    {
        builder.HasIndex(c => c.CodigoHash);
        builder.HasIndex(c => c.UsuarioId);
    }
}
