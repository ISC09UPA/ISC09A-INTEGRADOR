using AlmacenInteligente.Application.Abstracciones;
using AlmacenInteligente.Domain.Almacenes;
using AlmacenInteligente.Domain.Common;
using AlmacenInteligente.Domain.Documentos;
using AlmacenInteligente.Domain.Finanzas;
using AlmacenInteligente.Domain.Inventario;
using AlmacenInteligente.Domain.Productos;
using AlmacenInteligente.Domain.Recepciones;
using AlmacenInteligente.Domain.Terceros;
using AlmacenInteligente.Domain.Ventas;
using AlmacenInteligente.Infrastructure.Seguridad;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AlmacenInteligente.Infrastructure.Persistencia;

public sealed class AlmacenDbContext(DbContextOptions<AlmacenDbContext> options)
    : IdentityDbContext<Usuario, Rol, Guid>(options), IUnidadDeTrabajo
{
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Proveedor> Proveedores => Set<Proveedor>();
    public DbSet<Unidad> Unidades => Set<Unidad>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Almacen> Almacenes => Set<Almacen>();
    public DbSet<Existencia> Existencias => Set<Existencia>();
    public DbSet<MovimientoInventario> Kardex => Set<MovimientoInventario>();
    public DbSet<MovimientoFinanciero> MovimientosFinancieros => Set<MovimientoFinanciero>();
    public DbSet<Recepcion> Recepciones => Set<Recepcion>();
    public DbSet<Venta> Ventas => Set<Venta>();
    public DbSet<Traspaso> Traspasos => Set<Traspaso>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Ignore<Tercero>();
        modelBuilder.Ignore<Documento>();
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AlmacenDbContext).Assembly);

        // Tablas de Identity con el mismo estilo snake_case que el resto.
        modelBuilder.Entity<Usuario>().ToTable("usuarios");
        modelBuilder.Entity<Rol>().ToTable("roles");
        modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("usuarios_roles");
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("usuarios_claims");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("usuarios_logins");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("usuarios_tokens");
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("roles_claims");

        // Los eventos de dominio no se persisten como columnas; irán al outbox.
        foreach (var tipo in modelBuilder.Model.GetEntityTypes().Where(t => typeof(AggregateRoot).IsAssignableFrom(t.ClrType)))
            modelBuilder.Entity(tipo.ClrType).Ignore(nameof(AggregateRoot.Eventos));
    }

    /// <summary>PostgreSQL guarda timestamptz en UTC; Npgsql rechaza offsets distintos de cero, así que se convierten al escribir.</summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<ConvertidorUtc>();

    /// <summary>Traduce choques de concurrencia y de unicidad a mensajes que el usuario entiende.</summary>
    public async Task GuardarAsync(CancellationToken ct = default)
    {
        try
        {
            await SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new DomainException("Otra persona modificó las existencias al mismo tiempo. Revisa y vuelve a intentarlo.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
        {
            throw new DomainException("Ya existe un registro con esos datos.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.CheckViolation })
        {
            throw new DomainException("La operación dejaría una existencia negativa.");
        }
    }

    private sealed class ConvertidorUtc() : ValueConverter<DateTimeOffset, DateTimeOffset>(v => v.ToUniversalTime(), v => v);
}
