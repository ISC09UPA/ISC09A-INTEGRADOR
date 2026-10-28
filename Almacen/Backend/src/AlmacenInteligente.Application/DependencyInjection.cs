using AlmacenInteligente.Application.CasosDeUso;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace AlmacenInteligente.Application;

public static class DependencyInjection
{
    /// <summary>Registra el mediador, todos los manejadores y validadores de este ensamblado.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var ensamblado = typeof(DependencyInjection).Assembly;

        services.AddScoped<IMediador, Mediador>();
        services.AddValidatorsFromAssembly(ensamblado, ServiceLifetime.Scoped, includeInternalTypes: true);

        var manejadores = ensamblado.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IManejador<,>))
                .Select(i => (Servicio: i, Implementacion: t)));

        foreach (var (servicio, implementacion) in manejadores)
            services.AddScoped(servicio, implementacion);

        return services;
    }
}
