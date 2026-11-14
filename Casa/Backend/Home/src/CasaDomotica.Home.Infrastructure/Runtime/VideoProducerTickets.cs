using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CasaDomotica.Home.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CasaDomotica.Home.Infrastructure.Runtime;

public sealed record VideoProducerTicket(Guid SessionId, string UploadUrl, string UploadToken, DateTimeOffset ExpiresAtUtc);
public interface IVideoProducerTickets
{
    Task<VideoProducerTicket> PrepareAsync(CommandRow command, CancellationToken ct);
}
public sealed class VideoProducerTickets(HomeDb db, IConfiguration configuration, TimeProvider clock) : IVideoProducerTickets
{
    public async Task<VideoProducerTicket> PrepareAsync(CommandRow command, CancellationToken ct)
    {
        var root = configuration["Video:UploadBaseUrl"]?.TrimEnd('/');
        if (!Uri.TryCreate(root, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)) throw new HomeException("unavailable", "Video upload endpoint is not configured securely.");
        using var payload = JsonDocument.Parse(command.PayloadJson);
        var id = payload.RootElement.GetProperty("sessionId").GetGuid();
        var row = await db.VideoSessions.SingleOrDefaultAsync(x => x.SessionId == id && x.CommandId == command.CommandId && x.HouseId == command.HouseId && x.DeviceId == command.DeviceId && x.Active, ct);
        if (row is null || row.ExpiresAtUtc <= clock.GetUtcNow() || row.ProducerTokenHash is not null) throw new HomeException("unavailable", "Video producer session is unavailable.");
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        row.ProducerTokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        await db.SaveChangesAsync(ct);
        return new VideoProducerTicket(id, $"{root}/internal/video/{id:D}", token, row.ExpiresAtUtc);
    }
}
