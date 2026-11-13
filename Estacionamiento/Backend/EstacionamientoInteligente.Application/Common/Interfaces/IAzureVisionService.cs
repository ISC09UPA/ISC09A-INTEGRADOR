namespace AirportParking.Application.Common.Interfaces;

public interface IAzureVisionService
{
    Task<string?> AnalyzePlateImageAsync(byte[] imageBytes, CancellationToken cancellationToken = default);
    Task<string?> AnalyzePlateFromUrlAsync(string imageUrl, CancellationToken cancellationToken = default);
    Task<bool> PingAsync(CancellationToken cancellationToken = default);
}

