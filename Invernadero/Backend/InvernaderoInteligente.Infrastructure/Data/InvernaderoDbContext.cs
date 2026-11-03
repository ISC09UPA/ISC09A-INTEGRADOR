using Microsoft.EntityFrameworkCore;
using Telemetria.Models;

namespace Telemetria.Data;

public class TelemetriaDbContext : DbContext
{
    public TelemetriaDbContext(DbContextOptions<TelemetriaDbContext> options) : base(options) { }

    public DbSet<LecturaSensor> Lecturas => Set<LecturaSensor>();
    public DbSet<Riego> Riegos => Set<Riego>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LecturaSensor>(e =>
        {
            e.ToTable("LecturasSensor");
            e.HasIndex(x => new { x.SensorId, x.FechaHora });
            e.HasIndex(x => x.FechaHora);
        });

        modelBuilder.Entity<Riego>(e =>
        {
            e.ToTable("Riegos");
            e.HasIndex(x => x.FechaHoraInicio);
            e.HasIndex(x => new { x.InvernaderoId, x.Zona });
        });
    }
}