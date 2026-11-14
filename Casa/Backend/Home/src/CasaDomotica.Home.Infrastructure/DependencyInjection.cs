using CasaDomotica.Home.Domain.Interfaces;
using CasaDomotica.Home.Infrastructure.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
namespace CasaDomotica.Home.Infrastructure;
public static class DependencyInjection
{
    public static IServiceCollection AddHomeInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<HomeDb>(options => options.AddInterceptors(new SqlWarmupInterceptor()).UseSqlServer(configuration.GetConnectionString("HomeDb") ?? throw new InvalidOperationException("Configure ConnectionStrings:HomeDb through protected configuration.")));
        services.AddScoped<SqlHomeRuntime>();
        services.AddScoped<IHomeRuntime>(provider => provider.GetRequiredService<SqlHomeRuntime>());
        services.AddScoped<ICapabilityRuntime>(provider => provider.GetRequiredService<SqlHomeRuntime>());
        services.AddSingleton<HomeServiceTokenCache>();
        services.AddHttpClient<IdentityAuthorizer>(client => client.Timeout = TimeSpan.FromSeconds(15));
        services.AddScoped<IUserAuthorizer>(provider => provider.GetRequiredService<IdentityAuthorizer>());
        services.AddSingleton<IoTConnection>();
        services.AddSingleton<IDeviceTransport>(provider => provider.GetRequiredService<IoTConnection>());
        services.AddScoped<IDispatchQueue, SqlDispatchQueue>();
        services.AddScoped<ICaptureUploadTickets, CaptureUploadTickets>();
        services.AddScoped<IPrivateCaptureStore, PrivateCaptureStore>();
        services.AddScoped<CaptureUploadService>();
        services.AddScoped<VideoSessions>();
        services.AddScoped<IVideoProducerTickets, VideoProducerTickets>();
        services.AddScoped<IPrivateVideoStore, PrivateVideoStore>();
        services.AddScoped<VideoProducer>(); services.AddScoped<VideoPlayback>(); services.AddScoped<VideoCleanup>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<RuntimeWakeups>(); services.AddSingleton<BusConnection>(); services.AddScoped<OutboxPublisher>();
        services.AddScoped<IDeviceDispatcher, DeviceDispatcher>();
        services.AddScoped<CaptureVerifier>(); services.AddScoped<DeviceIngestion>(); services.AddScoped<HomeAdministration>();
        return services;
    }
}
