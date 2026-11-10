using CiudadInteligente.Application.Interfaces;
using CiudadInteligente.Application.Services;
using CiudadInteligente.Infrastructure.Hardware;
using CiudadInteligente.Infrastructure.Persistence;
using CiudadInteligente.Infrastructure.Persistence.Repositories;
using CiudadInteligente.Infrastructure.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CiudadInteligente.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registra el hardware (ESP32 por Serial, por la Raspberry Pi en la misma
    /// red o por Azure IoT Hub, segun "Esp32:Transporte") y la base de datos
    /// SQLite (Entity Framework Core).
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

        // ---- Base de datos: Entity Framework Core + SQLite (un DbContext por peticion) ----
        var cadena = config.GetConnectionString("CiudadInteligente") ?? "Data Source=ciudad_inteligente.db";
        services.AddDbContext<AppDbContext>(opciones => opciones.UseSqlite(cadena));
        services.AddScoped<IEmergenciaRepository, EmergenciaRepository>();
        services.AddScoped<IConteoTraficoRepository, ConteoTraficoRepository>();
        services.AddScoped<IUsuarioRepository, UsuarioRepository>();

        // ---- Seguridad y estadisticas ----
        services.AddSingleton<IHasherContrasenas, HasherContrasenas>();
        services.AddSingleton(new OpcionesEstadisticas(ZonaHoraria(config["Estadisticas:ZonaHoraria"])));

        return services;
    }

    /// <summary>
    /// Al arrancar: crea las tablas si no existen y abre de una vez la
    /// comunicacion con el ESP32 para empezar a recibir su estado (EST:).
    /// </summary>
    public static IServiceProvider IniciarInfraestructura(this IServiceProvider services)
    {
        using (var alcance = services.CreateScope())
        {
            var db = alcance.ServiceProvider.GetRequiredService<AppDbContext>();
            InicializadorBaseDeDatos.Inicializar(db);
            InicializadorUsuarios.Inicializar(
                db,
                services.GetRequiredService<IHasherContrasenas>(),
                services.GetRequiredService<IConfiguration>(),
                services.GetRequiredService<ILoggerFactory>().CreateLogger("CiudadInteligente.Usuarios"));
        }

        services.GetRequiredService<SemaforoControllerBase>();
        return services;
    }

    /// <summary>"America/Mexico_City" funciona en Windows y en Linux (.NET traduce los nombres IANA).</summary>
    private static TimeZoneInfo ZonaHoraria(string? id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(string.IsNullOrWhiteSpace(id) ? "America/Mexico_City" : id);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Local;
        }
    }
}
