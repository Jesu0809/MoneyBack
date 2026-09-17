using Microsoft.EntityFrameworkCore;
using MoneyBack.Api.Models;
using MoneyBack.Api.Models.Metas;

namespace MoneyBack.Api.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Hogar> Hogares => Set<Hogar>();
    public DbSet<MetaAhorro> MetasAhorro => Set<MetaAhorro>();
    public DbSet<MovimientoMeta> MovimientosMeta => Set<MovimientoMeta>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfiguration(new HogarConfiguration());
    }
}
