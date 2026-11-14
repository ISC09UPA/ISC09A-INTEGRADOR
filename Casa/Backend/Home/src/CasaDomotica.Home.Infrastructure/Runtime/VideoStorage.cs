using System.Security.Cryptography;
using System.Text;
using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CasaDomotica.Home.Domain.Interfaces;
using Microsoft.Extensions.Configuration;

namespace CasaDomotica.Home.Infrastructure.Runtime;

public interface IPrivateVideoStore
{
    Task<string?> FindAsync(VideoSegmentRow segment, byte[] bytes, CancellationToken ct);
    Task<string> CreateAsync(VideoSegmentRow segment, byte[] bytes, CancellationToken ct);
    Task<byte[]?> ReadAsync(VideoSegmentRow segment, CancellationToken ct);
    Task<bool> DeleteAsync(VideoSegmentRow segment, CancellationToken ct);
}
public sealed class PrivateVideoStore(IConfiguration configuration) : IPrivateVideoStore
{
    private BlobContainerClient Container()
    {
        var root = configuration["Storage:AccountUrl"] ?? throw new HomeException("unavailable", "Private video storage is not configured.");
        return new BlobServiceClient(new Uri(root), new DefaultAzureCredential()).GetBlobContainerClient("video-streams");
    }
    public async Task<string?> FindAsync(VideoSegmentRow segment, byte[] bytes, CancellationToken ct)
    {
        if (segment.BlobName is null) return null;
        var blob = Container().GetBlobClient(segment.BlobName);
        try
        {
            var properties = (await blob.GetPropertiesAsync(cancellationToken: ct)).Value;
            if (properties.ContentLength != bytes.Length || !properties.Metadata.TryGetValue("sessionid", out var session) || session != segment.SessionId.ToString("D") || !properties.Metadata.TryGetValue("sequence", out var sequence) || sequence != segment.Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture)) return null;
            var value = (await blob.DownloadContentAsync(new BlobRequestConditions { IfMatch = properties.ETag }, ct)).Value.Content.ToArray();
            var hash = Convert.ToHexString(SHA256.HashData(value));
            return value.Length == bytes.Length && hash == segment.ContentHash ? properties.ETag.ToString() : null;
        }
        catch (RequestFailedException error) when (error.Status is 404 or 412) { return null; }
    }
    public async Task<string> CreateAsync(VideoSegmentRow segment, byte[] bytes, CancellationToken ct)
    {
        var blob = Container().GetBlobClient(segment.BlobName ?? throw new InvalidOperationException("Video segment name is missing."));
        using var stream = new MemoryStream(bytes, writable: false);
        var options = new BlobUploadOptions { Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All }, HttpHeaders = new BlobHttpHeaders { ContentType = "video/mp2t" }, Metadata = new Dictionary<string, string> { ["sessionid"] = segment.SessionId.ToString("D"), ["sequence"] = segment.Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture), ["sha256"] = segment.ContentHash } };
        return (await blob.UploadAsync(stream, options, ct)).Value.ETag.ToString();
    }
    public async Task<byte[]?> ReadAsync(VideoSegmentRow segment, CancellationToken ct)
    {
        if (segment.BlobName is null || segment.BlobETag is null) return null;
        var blob = Container().GetBlobClient(segment.BlobName);
        try
        {
            var value = (await blob.DownloadContentAsync(new BlobRequestConditions { IfMatch = new ETag(segment.BlobETag) }, ct)).Value.Content.ToArray();
            return value.Length == segment.SizeBytes && value.Length <= VideoProducer.MaximumSegmentBytes ? value : null;
        }
        catch (RequestFailedException error) when (error.Status is 404 or 412) { return null; }
    }
    public async Task<bool> DeleteAsync(VideoSegmentRow segment, CancellationToken ct)
    {
        if (segment.BlobName is null) return true;
        var blob = Container().GetBlobClient(segment.BlobName);
        try
        {
            var properties = (await blob.GetPropertiesAsync(cancellationToken: ct)).Value;
            if (!properties.Metadata.TryGetValue("sessionid", out var session) || session != segment.SessionId.ToString("D") || !properties.Metadata.TryGetValue("sequence", out var sequence) || sequence != segment.Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture) || !properties.Metadata.TryGetValue("sha256", out var hash) || hash != segment.ContentHash) return false;
            if (segment.BlobETag is not null && properties.ETag.ToString() != segment.BlobETag) return false;
            await blob.DeleteIfExistsAsync(DeleteSnapshotsOption.None, new BlobRequestConditions { IfMatch = properties.ETag }, ct);
            return true;
        }
        catch (RequestFailedException error) when (error.Status == 404) { return true; }
        catch (RequestFailedException error) when (error.Status == 412) { return false; }
    }
}
