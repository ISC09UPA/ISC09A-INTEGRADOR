using CasaDomotica.Identity.Domain.Interfaces;
using CasaDomotica.Identity.Infrastructure.Persistence;
using CasaDomotica.Identity.Infrastructure.Security;
using CasaDomotica.Identity.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace CasaDomotica.Identity.Infrastructure;
public static class DependencyInjection
{
    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<IdentityDb>(options => options.AddInterceptors(new SqlWarmupInterceptor()).UseSqlServer(configuration.GetConnectionString("IdentityDb") ?? throw new InvalidOperationException("Configure ConnectionStrings:IdentityDb through protected configuration.")));
        services.AddScoped<SqlIdentityRepository>();
        services.AddScoped<IUserRepository>(sp => sp.GetRequiredService<SqlIdentityRepository>());
        services.AddScoped<ISessionRepository>(sp => sp.GetRequiredService<SqlIdentityRepository>());
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<JwtTokens>();
        services.AddSingleton<ITokenService>(sp => sp.GetRequiredService<JwtTokens>());
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddScoped<IdentityAdministration>();
        return services;
    }
}
