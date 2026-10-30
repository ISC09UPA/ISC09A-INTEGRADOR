using InvernaderoInteligente.Application.Interfaces;
using InvernaderoInteligente.Infrastructure.Data;
using InvernaderoInteligente.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InvernaderoInteligente.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registra el DbContext y los servicios de Infrastructure.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<InvernaderoDbContext>(opt => opt.UseSqlite(connectionString));
        // Si el equipo usa otra base de datos, cambiar UseSqlite por UseSqlServer / UseNpgsql
        // (y el paquete NuGet correspondiente en Infrastructure).

        services.AddScoped<ILecturaService, LecturaService>();
        services.AddScoped<IRiegoService, RiegoService>();

        return services;
    }
}
