using LockerSmart.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LockerSmart.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Locker> Lockers => Set<Locker>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Renta> Rentas => Set<Renta>();

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
        b.Entity<Renta>().Property(r => r.Estado).HasConversion<string>().HasMaxLength(20);
    }
}
