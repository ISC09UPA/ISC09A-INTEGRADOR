using System.Globalization;
using CiudadInteligente.Domain.Entities;
using CiudadInteligente.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CiudadInteligente.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core sobre SQLite. El mapeo coincide con las tablas que ya
/// existian (mismos nombres, fechas como texto ISO 8601 en UTC, tipo de
/// emergencia y rol como texto), asi que un archivo .db anterior sigue sirviendo.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> opciones) : DbContext(opciones)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Emergencia> Emergencias => Set<Emergencia>();
    public DbSet<ConteoTrafico> ConteosTrafico => Set<ConteoTrafico>();

    /// <summary>Siempre en UTC y formato "O": el orden alfabetico del texto es el orden de las fechas.</summary>
    private static readonly ValueConverter<DateTimeOffset, string> FechaATexto = new(
        fecha => fecha.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        texto => DateTimeOffset.Parse(texto, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));

    private static RolUsuario TextoARol(string texto) =>
        RolesUsuario.TryParse(texto, out var rol) ? rol : RolUsuario.Operador;

    private static readonly ValueConverter<RolUsuario, string> RolATexto = new(
        rol => RolesUsuario.Texto(rol),
        texto => TextoARol(texto));

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        modelo.Entity<Usuario>(e =>
        {
            e.ToTable("Usuarios", t => t.HasCheckConstraint("CK_Usuarios_Rol", "\"Rol\" IN ('administrador', 'operador')"));
            e.HasKey(u => u.Id);
            e.Property(u => u.Id).ValueGeneratedOnAdd();
            // "Admin" y "admin" son el mismo usuario
            e.Property(u => u.NombreUsuario).IsRequired().UseCollation("NOCASE");
            e.HasIndex(u => u.NombreUsuario).IsUnique();
            e.Property(u => u.NombreCompleto).IsRequired();
            e.Property(u => u.Rol).HasConversion(RolATexto).IsRequired();
            e.Property(u => u.HashContrasena).IsRequired();
            e.Property(u => u.CreadoEn).HasConversion(FechaATexto).IsRequired();
        });

        modelo.Entity<Emergencia>(e =>
        {
            e.ToTable("Emergencias", t => t.HasCheckConstraint("CK_Emergencias_Acceso", "\"Acceso\" BETWEEN 1 AND 4"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.Unidad).IsRequired();
            e.Property(x => x.Tipo).HasConversion<string>().IsRequired();
            e.Property(x => x.SolicitadaEn).HasConversion(FechaATexto).IsRequired();
            e.Property(x => x.FinalizadaEn).HasConversion(FechaATexto);
            e.Ignore(x => x.EstaActiva);
        });

        modelo.Entity<ConteoTrafico>(e =>
        {
            e.ToTable("ConteosTrafico");
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).ValueGeneratedOnAdd();
            e.Property(c => c.RegistradoEn).HasConversion(FechaATexto).IsRequired();
            e.HasIndex(c => c.RegistradoEn).HasDatabaseName("IX_ConteosTrafico_RegistradoEn");
            e.Ignore(c => c.Total);
        });
    }
}
