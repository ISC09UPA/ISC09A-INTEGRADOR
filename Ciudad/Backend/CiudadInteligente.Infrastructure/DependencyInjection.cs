using CiudadInteligente.Application.Interfaces;
using CiudadInteligente.Application.Services;
using Azure.Messaging.ServiceBus;
using CiudadInteligente.Infrastructure.Hardware;
using CiudadInteligente.Infrastructure.Correo;
using CiudadInteligente.Infrastructure.Mensajeria;
using CiudadInteligente.Infrastructure.Persistence;
using CiudadInteligente.Infrastructure.Persistence.Repositories;
using CiudadInteligente.Infrastructure.Seguridad;
using CiudadInteligente.Infrastructure.Vision;
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
    /// Azure SQL / SQL Server (Entity Framework Core).
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

        // ---- Base de datos: Entity Framework Core + Azure SQL (un DbContext por peticion) ----
        // En la nube: ConnectionStrings__CiudadInteligente (secreto). Local: LocalDB.
        var cadena = config.GetConnectionString("CiudadInteligente")
            ?? @"Server=(localdb)\MSSQLLocalDB;Database=CiudadInteligente;Trusted_Connection=True;TrustServerCertificate=True";
        services.AddDbContext<AppDbContext>(opciones => opciones.UseSqlServer(cadena, sql =>
            // Azure SQL corta conexiones de forma transitoria (sobre todo en serverless al "despertar")
            sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null)));
        services.AddScoped<IEmergenciaRepository, EmergenciaRepository>();
        services.AddScoped<IConteoTraficoRepository, ConteoTraficoRepository>();
        services.AddScoped<IUsuarioRepository, UsuarioRepository>();

        // ---- Azure Service Bus: cola de eventos del cruce (apagado salvo ServiceBus:Habilitado = true) ----
        var sb = new OpcionesServiceBus(
            bool.TryParse(config["ServiceBus:Habilitado"], out var sbActivo) && sbActivo,
            config["ServiceBus:Cola"] ?? "integrador",
            config["ServiceBus:ConnectionString"] ?? string.Empty);
        if (sb.EstaListo)
        {
            services.AddSingleton(sb);
            services.AddSingleton(new ServiceBusClient(sb.ConnectionString));
            services.AddSingleton<IPublicadorEventos, ServiceBusPublicadorEventos>();
            services.AddHostedService<ServiceBusConsumidorEventos>();
        }
        else
        {
            services.AddSingleton<IPublicadorEventos, PublicadorEventosNulo>();
        }

        // ---- Correo: Azure Communication Services Email (apagado salvo Correo:Habilitado = true) ----
        var correo = new OpcionesCorreo(
            bool.TryParse(config["Correo:Habilitado"], out var correoActivo) && correoActivo,
            config["Correo:ConnectionString"] ?? string.Empty,
            config["Correo:Remitente"] ?? string.Empty,
            (config["Correo:Destinatarios"] ?? string.Empty)
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (correo.EstaListo)
        {
            services.AddSingleton(correo);
            services.AddSingleton<IServicioCorreo, AzureServicioCorreo>();
        }
        else
        {
            services.AddSingleton<IServicioCorreo, ServicioCorreoNulo>();
        }

        // ---- Vision: Azure Custom Vision (apagado si faltan Vision:PredictionUrl / PredictionKey) ----
        services.AddSingleton(new OpcionesVision(
            config["Vision:PredictionUrl"] ?? string.Empty,
            config["Vision:PredictionKey"] ?? string.Empty,
            double.TryParse(config["Vision:Umbral"], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var umbral) ? umbral : 0.65));
        services.AddHttpClient<IServicioVision, CustomVisionServicio>(c => c.Timeout = TimeSpan.FromSeconds(20));

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
