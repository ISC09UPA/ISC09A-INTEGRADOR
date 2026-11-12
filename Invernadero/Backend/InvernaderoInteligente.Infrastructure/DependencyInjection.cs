using InvernaderoInteligente.Application.Interfaces;
using InvernaderoInteligente.Infrastructure.Data;
using InvernaderoInteligente.Infrastructure.DataAccess;
using InvernaderoInteligente.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InvernaderoInteligente.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registra el DbContext y los servicios de Infrastructure.</summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString,
        bool usarSqlServer)
    {
        services.AddDbContext<InvernaderoDbContext>(opt =>
        {
            if (usarSqlServer)
                // Reintenta si la base serverless está pausada o hay un corte breve de red
                opt.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure());
            else
                opt.UseSqlite(connectionString);
        });

        services.AddScoped<ILecturaDao, LecturaDao>();
        services.AddScoped<IRiegoDao, RiegoDao>();
        services.AddScoped<ILecturaService, LecturaService>();
        services.AddScoped<IRiegoService, RiegoService>();

        return services;
    }
}
