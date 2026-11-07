using CiudadInteligente.Application.Interfaces;
using CiudadInteligente.Infrastructure.Hardware;
using CiudadInteligente.Infrastructure.Persistence;
using CiudadInteligente.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CiudadInteligente.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registra el hardware (ESP32 por Serial, por la Raspberry Pi en la misma
    /// red o por Azure IoT Hub, segun "Esp32:Transporte") y la base de datos
    /// SQLite (Dapper).
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // ---- Hardware: una sola instancia para toda la app (un solo puerto/conexion) ----
        var transporte = config["Esp32:Transporte"] ?? "RaspberryPi";
        if (transporte.Equals("Serial", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<SemaforoControllerBase, SerialSemaforoController>();
        }
        else if (transporte.Equals("RaspberryPi", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<SemaforoControllerBase, RaspberryPiSemaforoController>();
        }
        else if (transporte.Equals("IoTHub", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<SemaforoControllerBase, IoTHubSemaforoController>();
        }
        else
        {
            throw new InvalidOperationException(
                $"Esp32:Transporte = \"{transporte}\" no es valido. Usa \"RaspberryPi\", \"Serial\" o \"IoTHub\".");
        }

        services.AddSingleton<ISemaforoController>(sp => sp.GetRequiredService<SemaforoControllerBase>());
        services.AddSingleton<IEstadoCruceProvider>(sp => sp.GetRequiredService<SemaforoControllerBase>());

        // ---- Base de datos: Dapper sobre ADO.NET (Microsoft.Data.Sqlite) ----
        var cadena = config.GetConnectionString("CiudadInteligente") ?? "Data Source=ciudad_inteligente.db";
        services.AddSingleton(new SqliteConnectionFactory(cadena));
        services.AddScoped<IEmergenciaRepository, EmergenciaRepository>();
        services.AddScoped<IConteoTraficoRepository, ConteoTraficoRepository>();

        return services;
    }

    /// <summary>
    /// Al arrancar: crea las tablas si no existen y abre de una vez la
    /// comunicacion con el ESP32 para empezar a recibir su estado (EST:).
    /// </summary>
    public static IServiceProvider IniciarInfraestructura(this IServiceProvider services)
    {
        InicializadorBaseDeDatos.Inicializar(services.GetRequiredService<SqliteConnectionFactory>());
        services.GetRequiredService<SemaforoControllerBase>();
        return services;
    }
}
