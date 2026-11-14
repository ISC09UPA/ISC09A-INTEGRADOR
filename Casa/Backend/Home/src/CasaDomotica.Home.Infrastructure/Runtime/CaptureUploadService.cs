using System.Data;
using System.Security.Cryptography;
using System.Text;
using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CasaDomotica.Home.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CasaDomotica.Home.Infrastructure.Runtime;

public sealed record CaptureUploadResult(Guid CaptureId, string BlobName);
public interface IPrivateCaptureStore
{
    Task<string> CreateAsync(string name, byte[] bytes, string contentType, CancellationToken ct);
}
public sealed class PrivateCaptureStore(IConfiguration configuration) : IPrivateCaptureStore
{
    public async Task<string> CreateAsync(string name, byte[] bytes, string contentType, CancellationToken ct)
    {
        var root = configuration["Storage:AccountUrl"] ?? throw new HomeException("unavailable", "Private storage is not configured.");
        var client = new BlobServiceClient(new Uri(root), new DefaultAzureCredential()).GetBlobContainerClient("captures").GetBlobClient(name);
        using var stream = new MemoryStream(bytes, writable: false);
        var response = await client.UploadAsync(stream, new BlobUploadOptions { Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All }, HttpHeaders = new BlobHttpHeaders { ContentType = contentType } }, ct);
        return response.Value.ETag.ToString();
    }
}

public sealed class CaptureUploadService(HomeDb db, IUserAuthorizer authorizer, IPrivateCaptureStore storage, TimeProvider clock)
{
    public async Task<CaptureUploadResult> UploadAsync(Guid captureId, string token, string contentType, long? length, Stream input, CancellationToken ct)
    {
        if (captureId == Guid.Empty || token.Length is < 40 or > 64) throw new HomeException("unauthorized", "Capture upload is not authorized.");
        if (contentType is not ("image/jpeg" or "image/png") || length is null or <= 0 or > CaptureImageValidator.MaximumBytes) throw new HomeException("invalidPayload", "Capture type or size is invalid.");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        var job = await db.CaptureJobs.AsNoTracking().SingleOrDefaultAsync(x => x.CaptureId == captureId, ct);
        if (job?.UploadTokenHash is null || !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(job.UploadTokenHash), Encoding.ASCII.GetBytes(hash))) throw new HomeException("unauthorized", "Capture upload is not authorized.");
        var command = await db.Commands.AsNoTracking().SingleOrDefaultAsync(x => x.CommandId == job.CommandId && x.HouseId == job.HouseId && x.DeviceId == job.DeviceId && x.Type == "CaptureImage", ct);
        if (command?.DispatchedAtUtc is null || command.Status is not ("pending" or "unknown" or "executed")) throw new HomeException("unauthorized", "Capture command is not eligible.");
        if (!await authorizer.CheckAsync(new(command.UserId, command.SessionId), command.HouseId, "home:control:capture", command.CorrelationId, ct)) throw new HomeException("forbidden", "Capture permission was revoked.");
        if (job.ExpiresAtUtc <= clock.GetUtcNow()) throw new HomeException("requestExpired", "Capture upload expired.");
        if (job.UploadStatus == "uploaded" && job.UploadedBlobName is not null)
        {
            var repeated = await ReadBoundedAsync(input, length.Value, ct);
            var repeatedHash = Convert.ToHexString(SHA256.HashData(repeated));
            if (job.UploadedContentType != contentType || job.UploadedContentHash != repeatedHash) throw new HomeException("duplicateChanged", "The upload token was already used for different content.");
            return new(captureId, job.UploadedBlobName);
        }
        // Claim only one upload. A lost connection never causes a second overwrite.
        var claimed = await db.CaptureJobs.Where(x => x.CaptureId == captureId && x.UploadTokenHash == hash && x.UploadStatus == "pending" && x.ExpiresAtUtc == job.ExpiresAtUtc && db.Commands.Any(c => c.CommandId == x.CommandId && c.DispatchedAtUtc != null && (c.Status == "pending" || c.Status == "unknown" || c.Status == "executed"))).ExecuteUpdateAsync(update => update.SetProperty(x => x.UploadStatus, "uploading"), ct);
        if (claimed != 1) throw new HomeException("conflict", "Capture upload is already in progress or unavailable.");
        try
        {
            var bytes = await ReadBoundedAsync(input, length.Value, ct);
            var extension = CaptureImageValidator.Validate(bytes, contentType);
            var contentHash = Convert.ToHexString(SHA256.HashData(bytes));
            if (!await authorizer.CheckAsync(new(command.UserId, command.SessionId), command.HouseId, "home:control:capture", command.CorrelationId, ct)) throw new HomeException("forbidden", "Capture permission was revoked.");
            if (job.ExpiresAtUtc <= clock.GetUtcNow()) throw new HomeException("requestExpired", "Capture upload expired.");
            if (!await db.Commands.AsNoTracking().AnyAsync(x => x.CommandId == command.CommandId && x.DispatchedAtUtc != null && (x.Status == "pending" || x.Status == "unknown" || x.Status == "executed"), ct)) throw new HomeException("unauthorized", "Capture command is no longer eligible.");
            var name = $"{job.HouseId:D}/{job.DeviceId}/{job.CaptureId:D}.{extension}";
            var etag = await storage.CreateAsync(name, bytes, contentType, ct);
            if (job.ExpiresAtUtc <= clock.GetUtcNow()) throw new HomeException("requestExpired", "Capture upload expired.");
            var changed = await db.CaptureJobs.Where(x => x.CaptureId == captureId && x.UploadTokenHash == hash && x.UploadStatus == "uploading" && db.Commands.Any(c => c.CommandId == x.CommandId && c.DispatchedAtUtc != null && (c.Status == "pending" || c.Status == "unknown" || c.Status == "executed"))).ExecuteUpdateAsync(update => update.SetProperty(x => x.UploadStatus, "uploaded").SetProperty(x => x.UploadedBlobName, name).SetProperty(x => x.UploadedETag, etag).SetProperty(x => x.UploadedContentHash, contentHash).SetProperty(x => x.UploadedContentType, contentType), ct);
            if (changed != 1) throw new HomeException("unavailable", "Capture upload could not be recorded.");
            return new(captureId, name);
        }
        catch
        {
            // A partial or ambiguous upload stays unavailable; never replace evidence.
            try { await db.CaptureJobs.Where(x => x.CaptureId == captureId && x.UploadStatus == "uploading").ExecuteUpdateAsync(update => update.SetProperty(x => x.UploadStatus, "failed"), CancellationToken.None); } catch { }
            throw;
        }
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream input, long length, CancellationToken ct)
    {
        using var buffer = new MemoryStream((int)length);
        var chunk = new byte[64 * 1024]; long total = 0;
        while (true)
        {
            var read = await input.ReadAsync(chunk, ct);
            if (read == 0) break;
            total += read;
            if (total > length || total > CaptureImageValidator.MaximumBytes) throw new HomeException("invalidPayload", "Capture body exceeds its declared size.");
            await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
        }
        if (total != length) throw new HomeException("invalidPayload", "Capture body is incomplete.");
        return buffer.ToArray();
    }
}
