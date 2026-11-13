namespace AirportParking.Infrastructure.Azure;

public class AzureSettings
{
    public const string SectionName = "Azure";

    public ComputerVisionSettings ComputerVision { get; set; } = new();
    public CommunicationServicesSettings CommunicationServices { get; set; } = new();
    public ServiceBusSettings ServiceBus { get; set; } = new();
    public BlobStorageSettings BlobStorage { get; set; } = new();
}

public class ComputerVisionSettings
{
    public string Endpoint { get; set; } = "https://centralus.api.cognitive.microsoft.com/";
    public string SubscriptionKey { get; set; } = string.Empty;
    public string Region { get; set; } = "centralus";
}

public class CommunicationServicesSettings
{
    public string Endpoint { get; set; } = "https://comunicacion9no.unitedstates.communication.azure.com/";
    public string AccessKey { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;
}

public class ServiceBusSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public string Namespace { get; set; } = "bus9no";
    public string KeyName { get; set; } = "RootManageSharedAccessKey";
    public string Key { get; set; } = string.Empty;
    public string QueueName { get; set; } = "estacionamiento-events";
}

public class BlobStorageSettings
{
    public string AccountName { get; set; } = "storage9no";
    public string AccountKey { get; set; } = string.Empty;
    public string ContainerName { get; set; } = "lpr-evidencias";
    public string ConnectionString { get; set; } = string.Empty;
}
