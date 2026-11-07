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
        // Local: "Data Source=invernadero.db" → SQLite.
        // Azure: cadena ADO.NET de Azure SQL ("Server=tcp:...") → SQL Server.
        services.AddDbContext<InvernaderoDbContext>(opt =>
        {
            if (EsSqlServer(connectionString))
                // Reintenta si la base serverless está pausada o hay un corte breve de red
                opt.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure());
            else
                opt.UseSqlite(connectionString);
        });

        services.AddScoped<ILecturaService, LecturaService>();
        services.AddScoped<IRiegoService, RiegoService>();

        return services;
    }

    private static bool EsSqlServer(string connectionString) =>
        connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase) ||
        connectionString.Contains("Initial Catalog=", StringComparison.OrdinalIgnoreCase);
}
