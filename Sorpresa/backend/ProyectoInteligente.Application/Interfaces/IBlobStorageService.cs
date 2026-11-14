namespace LockerSmart.Application.Interfaces;

public interface IBlobStorageService
{
    Task<string> UploadAsync(Stream content, string contentType, CancellationToken cancellationToken = default);
    Task<string> CreateReadSasUrlAsync(string blobName, TimeSpan validity, CancellationToken cancellationToken = default);
    Task DeleteAsync(string blobName, CancellationToken cancellationToken = default);
}
