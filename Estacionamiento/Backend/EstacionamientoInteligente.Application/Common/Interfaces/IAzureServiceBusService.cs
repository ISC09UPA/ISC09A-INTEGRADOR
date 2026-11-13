namespace AirportParking.Application.Common.Interfaces;

public interface IAzureServiceBusService
{
    Task<bool> PublishEventAsync<T>(string eventType, T payload, CancellationToken cancellationToken = default);
    Task<bool> PingAsync(CancellationToken cancellationToken = default);
}

