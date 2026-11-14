using System.Text.Json;
using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CasaDomotica.Home.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
namespace CasaDomotica.Home.Infrastructure.Runtime;
public sealed class CaptureVerifier(HomeDb db, IConfiguration configuration)
{
    public async Task<string> VerifyAsync(string deviceId, JsonElement payload, DateTimeOffset occurredAt, CancellationToken ct)
    {
        var captureId = payload.GetProperty("captureId").GetGuid();
        var job = await db.CaptureJobs.AsNoTracking().SingleOrDefaultAsync(x => x.CaptureId == captureId && x.DeviceId == deviceId, ct);
        if (job is null) throw new HomeException("invalidEvent", "No authorized capture job matches this file.");
        if (job.CreatedAtUtc is null || occurredAt < job.CreatedAtUtc.Value.AddSeconds(-30) || occurredAt > job.ExpiresAtUtc) throw new HomeException("invalidEvent", "The capture does not match the authorized time window.");
        var command = await db.Commands.AsNoTracking().SingleOrDefaultAsync(x => x.CommandId == job.CommandId && x.HouseId == job.HouseId && x.DeviceId == deviceId && x.Type == "CaptureImage", ct);
        if (command?.DispatchedAtUtc is null || occurredAt < command.DispatchedAtUtc.Value.AddSeconds(-30) || command.Status is not ("pending" or "unknown" or "executed")) throw new HomeException("invalidEvent", "No eligible dispatched capture command matches this file.");
        var name = payload.GetProperty("blobName").GetString()!;
        var prefix = $"{job.HouseId:D}/{deviceId}/{captureId:D}.";
        if (!name.StartsWith(prefix, StringComparison.Ordinal) || name[prefix.Length..] is not ("jpg" or "png")) throw new HomeException("invalidEvent", "Capture path does not match its authorized owner.");
        if (job.UploadStatus != "uploaded" || job.UploadedBlobName != name || string.IsNullOrEmpty(job.UploadedETag)) throw new HomeException("capturePending", "Capture upload has not been confirmed.");
        var root = configuration["Storage:AccountUrl"] ?? throw new HomeException("unavailable", "Private capture storage is not configured.");
        var container = new BlobServiceClient(new Uri(root), new DefaultAzureCredential()).GetBlobContainerClient("captures");
        try
        {
            var blob = container.GetBlobClient(name);
            var properties = (await blob.GetPropertiesAsync(cancellationToken: ct)).Value;
            if (properties.ETag.ToString() != job.UploadedETag) throw new HomeException("invalidEvent", "Capture content changed after upload.");
            if (properties.ContentLength is <= 0 or > 5242880 || properties.ContentType is not ("image/jpeg" or "image/png")) throw new HomeException("invalidEvent", "Capture format or size is invalid.");
            // The ETag condition prevents a different blob version from being validated
            // after its properties were read. Never trust content type alone.
            var data = (await blob.DownloadContentAsync(new BlobRequestConditions { IfMatch = properties.ETag }, ct)).Value.Content.ToArray();
            try
            {
                var extension = CaptureImageValidator.Validate(data, properties.ContentType);
                if (!name.EndsWith("." + extension, StringComparison.Ordinal)) throw new HomeException("invalidEvent", "Capture extension does not match its content.");
            }
            catch (HomeException error) when (error.Code == "invalidPayload") { throw new HomeException("invalidEvent", "Capture content is invalid."); }
            return properties.ETag.ToString();
        }
        catch (RequestFailedException error) when (error.Status == 404) { throw new HomeException("capturePending", "The capture file is not available yet."); }
        catch (RequestFailedException error) when (error.Status == 412) { throw new HomeException("invalidEvent", "Capture content changed during verification."); }
    }
}
