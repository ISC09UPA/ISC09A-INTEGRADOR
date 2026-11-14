using LockerSmart.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LockerSmart.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Locker> Lockers => Set<Locker>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Renta> Rentas => Set<Renta>();
    public DbSet<EventoDispositivo> EventosDispositivo => Set<EventoDispositivo>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Usuario>(e =>
        {
            e.Property(u => u.Email).HasMaxLength(200);
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Rol).HasConversion<string>().HasMaxLength(20);
        });
        b.Entity<Locker>(e =>
        {
            e.Property(l => l.Codigo).HasMaxLength(20);
            e.HasIndex(l => l.Codigo).IsUnique();
            e.Property(l => l.Estado).HasConversion<string>().HasMaxLength(20);
        });
        b.Entity<Renta>(e =>
        {
            e.Property(r => r.Estado)
                .HasConversion<string>()
                .HasMaxLength(20);

            e.HasOne<Usuario>()
                .WithMany()
                .HasForeignKey(r => r.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne<Locker>()
                .WithMany()
                .HasForeignKey(r => r.LockerId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(r => r.UsuarioId);
            e.HasIndex(r => r.LockerId);
        });
        b.Entity<Renta>().Property(r => r.Estado).HasConversion<string>().HasMaxLength(20);
    }
}
