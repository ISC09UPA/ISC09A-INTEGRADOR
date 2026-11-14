using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;

namespace CasaDomotica.Security.Infrastructure;

public sealed record CaptureContent(byte[] Bytes, string ContentType);
public interface ICaptureContent
{
    Task<CaptureContent?> ReadAsync(CaptureRow capture, CancellationToken ct);
    Task<bool> DeleteAsync(CaptureRow capture, CancellationToken ct);
}
public sealed class PrivateCaptureContent(IConfiguration configuration) : ICaptureContent
{
    public async Task<CaptureContent?> ReadAsync(CaptureRow capture, CancellationToken ct)
    {
        var root = configuration["Storage:AccountUrl"] ?? throw new InvalidOperationException("Private capture storage is not configured.");
        var blob = new BlobServiceClient(new Uri(root), new DefaultAzureCredential()).GetBlobContainerClient("captures").GetBlobClient(capture.BlobName);
        try
        {
            var result = (await blob.DownloadContentAsync(new BlobRequestConditions { IfMatch = new ETag(capture.BlobETag) }, ct)).Value;
            var bytes = result.Content.ToArray();
            if (bytes.Length is <= 0 or > 5242880) throw new InvalidDataException("Capture size is invalid.");
            var contentType = capture.BlobName.EndsWith(".png", StringComparison.Ordinal) ? "image/png" : "image/jpeg";
            return new CaptureContent(bytes, contentType);
        }
        catch (RequestFailedException error) when (error.Status is 404 or 412) { return null; }
    }
    public async Task<bool> DeleteAsync(CaptureRow capture, CancellationToken ct)
    {
        var root = configuration["Storage:AccountUrl"] ?? throw new InvalidOperationException("Private capture storage is not configured.");
        var blob = new BlobServiceClient(new Uri(root), new DefaultAzureCredential()).GetBlobContainerClient("captures").GetBlobClient(capture.BlobName);
        try
        {
            // Never delete a replacement blob; only the version verified by Home.
            await blob.DeleteIfExistsAsync(DeleteSnapshotsOption.None, new BlobRequestConditions { IfMatch = new ETag(capture.BlobETag) }, ct);
            return true;
        }
        catch (RequestFailedException error) when (error.Status == 412) { return false; }
    }
}
