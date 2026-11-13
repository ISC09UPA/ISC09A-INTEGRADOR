namespace AirportParking.Application.Common.Interfaces;

public interface IAzureBlobService
{
    Task<string?> UploadEvidenceAsync(string blobName, byte[] data, string contentType = "image/jpeg", CancellationToken cancellationToken = default);
    Task<bool> PingAsync(CancellationToken cancellationToken = default);
}

