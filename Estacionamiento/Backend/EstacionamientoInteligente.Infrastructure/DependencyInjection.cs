using AirportParking.Application.Common.Interfaces;
using AirportParking.Infrastructure.Azure;
using AirportParking.Infrastructure.Cryptography;
using AirportParking.Infrastructure.Persistence;
using AirportParking.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AirportParking.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        Action<AzureSettings>? configureAzure = null)
    {
        services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();
        services.AddSingleton<ICryptographyService, Aes256GcmCryptographyService>();
        services.AddSingleton<IParkingStore, InMemoryParkingStore>();
        services.AddScoped<IAccessDecisionService, AccessDecisionService>();

        // Cargar archivo .env local si existe
        LoadDotEnvFile();

        var azureSettings = new AzureSettings();

        // 1. Cargar variables de entorno
        LoadFromEnvironment(azureSettings);

        // 2. Aplicar configuración manual / lambda si se especifica
        configureAzure?.Invoke(azureSettings);

        // Registrar Azure Settings
        services.AddSingleton(azureSettings);

        // Registrar HttpClient genérico
        services.AddSingleton<HttpClient>();

        // Registrar Servicios Azure
        services.AddSingleton<IAzureVisionService, AzureVisionService>();
        services.AddSingleton<IAzureCommunicationService, AzureCommunicationService>();
        services.AddSingleton<IAzureServiceBusService, AzureServiceBusService>();
        services.AddSingleton<IAzureBlobService, AzureBlobService>();

        return services;
    }

    private static void LoadDotEnvFile()
    {
        try
        {
            var searchDirs = new[]
            {
                Directory.GetCurrentDirectory(),
                AppDomain.CurrentDomain.BaseDirectory,
                Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..")),
                Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "../..")),
                Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "../../..")),
                Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "../../../.."))
            };

            foreach (var dir in searchDirs)
            {
                var envPath = Path.Combine(dir, ".env");
                if (File.Exists(envPath))
                {
                    foreach (var line in File.ReadAllLines(envPath))
                    {
                        var trimmed = line.Trim();
                        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#') || !trimmed.Contains('='))
                            continue;

                        var separatorIdx = trimmed.IndexOf('=');
                        var key = trimmed.Substring(0, separatorIdx).Trim();
                        var val = trimmed.Substring(separatorIdx + 1).Trim().Trim('"').Trim('\'');

                        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                        {
                            Environment.SetEnvironmentVariable(key, val);
                        }
                    }
                    break;
                }
            }
        }
        catch
        {
            // Silencioso en caso de restricciones de I/O
        }
    }

    private static void LoadFromEnvironment(AzureSettings settings)
    {
        // Computer Vision
        var cvKey = Environment.GetEnvironmentVariable("AZURE_COMPUTER_VISION_KEY");
        if (!string.IsNullOrEmpty(cvKey)) settings.ComputerVision.SubscriptionKey = cvKey;

        var cvEndpoint = Environment.GetEnvironmentVariable("AZURE_COMPUTER_VISION_ENDPOINT");
        if (!string.IsNullOrEmpty(cvEndpoint)) settings.ComputerVision.Endpoint = cvEndpoint;

        var cvRegion = Environment.GetEnvironmentVariable("AZURE_COMPUTER_VISION_REGION");
        if (!string.IsNullOrEmpty(cvRegion)) settings.ComputerVision.Region = cvRegion;

        // Communication Services
        var acsKey = Environment.GetEnvironmentVariable("AZURE_COMMUNICATION_KEY");
        if (!string.IsNullOrEmpty(acsKey)) settings.CommunicationServices.AccessKey = acsKey;

        var acsEndpoint = Environment.GetEnvironmentVariable("AZURE_COMMUNICATION_ENDPOINT");
        if (!string.IsNullOrEmpty(acsEndpoint)) settings.CommunicationServices.Endpoint = acsEndpoint;

        var acsConn = Environment.GetEnvironmentVariable("AZURE_COMMUNICATION_CONNECTION_STRING");
        if (!string.IsNullOrEmpty(acsConn)) settings.CommunicationServices.ConnectionString = acsConn;

        // Service Bus
        var sbKey = Environment.GetEnvironmentVariable("AZURE_SERVICE_BUS_KEY");
        if (!string.IsNullOrEmpty(sbKey)) settings.ServiceBus.Key = sbKey;

        var sbConn = Environment.GetEnvironmentVariable("AZURE_SERVICE_BUS_CONNECTION_STRING");
        if (!string.IsNullOrEmpty(sbConn)) settings.ServiceBus.ConnectionString = sbConn;

        var sbNs = Environment.GetEnvironmentVariable("AZURE_SERVICE_BUS_NAMESPACE");
        if (!string.IsNullOrEmpty(sbNs)) settings.ServiceBus.Namespace = sbNs;

        var sbQueue = Environment.GetEnvironmentVariable("AZURE_SERVICE_BUS_QUEUE");
        if (!string.IsNullOrEmpty(sbQueue)) settings.ServiceBus.QueueName = sbQueue;

        // Blob Storage
        var blobKey = Environment.GetEnvironmentVariable("AZURE_STORAGE_KEY");
        if (!string.IsNullOrEmpty(blobKey)) settings.BlobStorage.AccountKey = blobKey;

        var blobAcc = Environment.GetEnvironmentVariable("AZURE_STORAGE_ACCOUNT");
        if (!string.IsNullOrEmpty(blobAcc)) settings.BlobStorage.AccountName = blobAcc;

        var blobCont = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONTAINER");
        if (!string.IsNullOrEmpty(blobCont)) settings.BlobStorage.ContainerName = blobCont;

        var blobConn = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");
        if (!string.IsNullOrEmpty(blobConn)) settings.BlobStorage.ConnectionString = blobConn;
    }
}
