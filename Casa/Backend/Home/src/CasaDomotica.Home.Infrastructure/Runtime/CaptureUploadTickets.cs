using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CasaDomotica.Home.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CasaDomotica.Home.Infrastructure.Runtime;

public sealed record CaptureUploadTicket(Guid CaptureId, string Url, string Token, DateTimeOffset ExpiresAtUtc);
public interface ICaptureUploadTickets
{
    Task<CaptureUploadTicket> PrepareAsync(CommandRow command, CancellationToken ct);
}

public sealed class CaptureUploadTickets(HomeDb db, IConfiguration configuration, TimeProvider clock) : ICaptureUploadTickets
{
    public async Task<CaptureUploadTicket> PrepareAsync(CommandRow command, CancellationToken ct)
    {
        var root = configuration["Capture:UploadBaseUrl"]?.TrimEnd('/');
        if (!Uri.TryCreate(root, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new HomeException("unavailable", "Capture upload endpoint is not configured securely.");
        using var payload = JsonDocument.Parse(command.PayloadJson);
        var id = payload.RootElement.GetProperty("captureId").GetGuid();
        var job = await db.CaptureJobs.SingleOrDefaultAsync(x => x.CaptureId == id && x.CommandId == command.CommandId && x.HouseId == command.HouseId && x.DeviceId == command.DeviceId, ct);
        if (job is null || job.ExpiresAtUtc <= clock.GetUtcNow() || job.UploadTokenHash is not null) throw new HomeException("unavailable", "Capture upload job is unavailable.");
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        job.UploadTokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        job.UploadStatus = "pending";
        await db.SaveChangesAsync(ct);
        return new CaptureUploadTicket(id, $"{root}/internal/captures/{id:D}/upload", token, job.ExpiresAtUtc);
    }
}
