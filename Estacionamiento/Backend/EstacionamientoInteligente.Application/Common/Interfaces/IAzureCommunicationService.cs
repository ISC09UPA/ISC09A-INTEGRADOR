namespace AirportParking.Application.Common.Interfaces;

public interface IAzureCommunicationService
{
    Task<string?> CreateIdentityAsync(CancellationToken cancellationToken = default);
    Task<bool> SendSecurityAlertAsync(string alertMessage, string? recipientPhone = null, CancellationToken cancellationToken = default);
    Task<bool> PingAsync(CancellationToken cancellationToken = default);
}

