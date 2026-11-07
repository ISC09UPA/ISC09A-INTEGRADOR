using CiudadInteligente.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CiudadInteligente.Application;

public static class DependencyInjection
{
    /// <summary>Registra los servicios (casos de uso) de la capa Application.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ISemaforoService, SemaforoService>();
        services.AddScoped<IEmergenciaService, EmergenciaService>();
        services.AddScoped<ITraficoService, TraficoService>();
        return services;
    }
}
