using AirportParking.Application.Common.Interfaces;
using AirportParking.Infrastructure.Cryptography;
using AirportParking.Infrastructure.Persistence;
using AirportParking.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AirportParking.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();
        services.AddSingleton<ICryptographyService, Aes256GcmCryptographyService>();
        services.AddSingleton<IParkingStore, InMemoryParkingStore>();
        services.AddScoped<IAccessDecisionService, AccessDecisionService>();

        return services;
    }
}
