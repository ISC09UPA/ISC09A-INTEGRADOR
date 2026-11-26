using CiudadInteligente.Domain.Entities;
using CiudadInteligente.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CiudadInteligente.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core sobre SQL Server / Azure SQL. Las fechas son
/// datetimeoffset nativas; el tipo de emergencia y el rol se guardan como texto.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> opciones) : DbContext(opciones)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Emergencia> Emergencias => Set<Emergencia>();
    public DbSet<ConteoTrafico> ConteosTrafico => Set<ConteoTrafico>();

    private static RolUsuario TextoARol(string texto) =>
        RolesUsuario.TryParse(texto, out var rol) ? rol : RolUsuario.Operador;

    private static readonly ValueConverter<RolUsuario, string> RolATexto = new(
        rol => RolesUsuario.Texto(rol),
        texto => TextoARol(texto));

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        modelo.Entity<Usuario>(e =>
        {
            e.ToTable("Usuarios", t => t.HasCheckConstraint("CK_Usuarios_Rol", "[Rol] IN ('administrador', 'operador')"));
            e.HasKey(u => u.Id);
            e.Property(u => u.Id).ValueGeneratedOnAdd();
            // el collation por defecto de Azure SQL no distingue mayusculas: "Admin" y "admin" son el mismo usuario
            e.Property(u => u.NombreUsuario).IsRequired().HasMaxLength(100);
            e.HasIndex(u => u.NombreUsuario).IsUnique();
            e.Property(u => u.NombreCompleto).IsRequired().HasMaxLength(200);
            e.Property(u => u.Rol).HasConversion(RolATexto).HasMaxLength(30).IsRequired();
            e.Property(u => u.HashContrasena).IsRequired().HasMaxLength(500);
            e.Property(u => u.CreadoEn).IsRequired();
        });

        modelo.Entity<Emergencia>(e =>
        {
            e.ToTable("Emergencias", t => t.HasCheckConstraint("CK_Emergencias_Acceso", "[Acceso] BETWEEN 1 AND 4"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.Unidad).IsRequired().HasMaxLength(200);
            e.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(30).IsRequired();
            e.Property(x => x.SolicitadaEn).IsRequired();
            e.Ignore(x => x.EstaActiva);
        });

        modelo.Entity<ConteoTrafico>(e =>
        {
            e.ToTable("ConteosTrafico");
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).ValueGeneratedOnAdd();
            e.Property(c => c.RegistradoEn).IsRequired();
            e.HasIndex(c => c.RegistradoEn).HasDatabaseName("IX_ConteosTrafico_RegistradoEn");
            e.Ignore(c => c.Total);
        });
    }
}
