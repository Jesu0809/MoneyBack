using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MoneyBack.Api.Models;
using MoneyBack.Api.Models.Auth;
using MoneyBack.Api.Models.DiaADia;
using MoneyBack.Api.Models.Deudas;
using MoneyBack.Api.Models.Metas;
using MoneyBack.Api.Models.Notificaciones;
using MoneyBack.Api.Models.Suscripciones;

namespace MoneyBack.Api.Data;

public class ApplicationDbContext : IdentityDbContext<Usuario, IdentityRole<int>, int>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Hogar> Hogares => Set<Hogar>();
    public DbSet<MetaAhorro> MetasAhorro => Set<MetaAhorro>();
    public DbSet<MovimientoMeta> MovimientosMeta => Set<MovimientoMeta>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<CodigoInvitacion> CodigosInvitacion => Set<CodigoInvitacion>();
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<MovimientoDiaADia> MovimientosDiaADia => Set<MovimientoDiaADia>();
    public DbSet<Presupuesto> Presupuestos => Set<Presupuesto>();
    public DbSet<Suscripcion> Suscripciones => Set<Suscripcion>();
    public DbSet<ConfirmacionCobro> ConfirmacionesCobro => Set<ConfirmacionCobro>();
    public DbSet<Deuda> Deudas => Set<Deuda>();
    public DbSet<PagoDeuda> PagosDeuda => Set<PagoDeuda>();
    public DbSet<SuscripcionPush> SuscripcionesPush => Set<SuscripcionPush>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Usuario>().ToTable("Usuarios");
        builder.Entity<IdentityRole<int>>().ToTable("Roles");
        builder.Entity<IdentityUserRole<int>>().ToTable("UsuarioRoles");
        builder.Entity<IdentityUserClaim<int>>().ToTable("UsuarioClaims");
        builder.Entity<IdentityUserLogin<int>>().ToTable("UsuarioLogins");
        builder.Entity<IdentityUserToken<int>>().ToTable("UsuarioTokens");
        builder.Entity<IdentityRoleClaim<int>>().ToTable("RoleClaims");

        builder.ApplyConfiguration(new HogarConfiguration());
        builder.ApplyConfiguration(new PresupuestoConfiguration());
        builder.ApplyConfiguration(new ConfirmacionCobroConfiguration());
        builder.ApplyConfiguration(new DeudaConfiguration());
        builder.ApplyConfiguration(new SuscripcionPushConfiguration());
    }
}
