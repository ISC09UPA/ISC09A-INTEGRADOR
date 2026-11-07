using Microsoft.EntityFrameworkCore;
using InvernaderoInteligente.Domain.Entities;

namespace InvernaderoInteligente.Infrastructure.Data;

public class InvernaderoDbContext : DbContext
{
    public InvernaderoDbContext(DbContextOptions<InvernaderoDbContext> options) : base(options) { }

    public DbSet<LecturaSensor> Lecturas => Set<LecturaSensor>();
    public DbSet<Riego> Riegos => Set<Riego>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LecturaSensor>(e =>
        {
            e.ToTable("LecturasSensor");
            // Una misma lectura no puede guardarse dos veces (reintentos de la Function)
            e.HasIndex(x => new { x.SensorId, x.Tipo, x.FechaHora }).IsUnique();
            e.HasIndex(x => x.FechaHora);
        });

        modelBuilder.Entity<Riego>(e =>
        {
            e.ToTable("Riegos");
            e.HasIndex(x => x.FechaHoraInicio);
            e.HasIndex(x => new { x.InvernaderoId, x.Zona });
            // Un mismo riego no puede guardarse dos veces (reintentos de la Function)
            e.HasIndex(x => new { x.InvernaderoId, x.Zona, x.FechaHoraInicio }).IsUnique();
        });
    }
}
