using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MoneyBack.Api.Models.Auth;

public class TokenAtajoConfiguration : IEntityTypeConfiguration<TokenAtajo>
{
    public void Configure(EntityTypeBuilder<TokenAtajo> builder)
    {
        builder.HasIndex(t => t.TokenHash).IsUnique();
    }
}
