using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AlmacenInteligente.Infrastructure.Persistencia;

/// <summary>
/// Para "dotnet ef" sin depender de un host: dotnet ef migrations add X -p src/AlmacenInteligente.Infrastructure -s src/AlmacenInteligente.Infrastructure
/// No se conecta a la base para generar migraciones; la cadena solo importa para "database update".
/// </summary>
internal sealed class FabricaDisenio : IDesignTimeDbContextFactory<AlmacenDbContext>
{
    public AlmacenDbContext CreateDbContext(string[] args)
    {
        var cadena = Environment.GetEnvironmentVariable("ConnectionStrings__Almacen")
            ?? "Host=localhost;Port=5432;Database=almacen;Username=root";
        var opciones = new DbContextOptionsBuilder<AlmacenDbContext>()
            .UseNpgsql(cadena)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new AlmacenDbContext(opciones);
    }
}
