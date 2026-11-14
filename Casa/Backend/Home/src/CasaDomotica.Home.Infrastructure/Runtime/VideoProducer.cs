using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure;
using CasaDomotica.Home.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CasaDomotica.Home.Infrastructure.Runtime;

public sealed record VideoSegmentUploadResult(long Sequence, string Status);
public sealed class VideoProducer(HomeDb db, IUserAuthorizer authorizer, IPrivateVideoStore storage, TimeProvider clock)
{
    public const int MaximumSegmentBytes = 1024 * 1024;
    public const long MaximumSessionBytes = 96L * 1024 * 1024;
    public const int MaximumSegments = 120;

    public async Task<VideoSegmentUploadResult> UploadSegmentAsync(Guid sessionId, string token, long sequence, int durationMs, long? length, Stream input, CancellationToken ct)
    {
        if (sequence is < 0 or >= MaximumSegments || durationMs is < 250 or > 10000 || length is null or < 188 or > MaximumSegmentBytes || length % 188 != 0) throw new HomeException("invalidPayload", "Video segment length or duration is invalid.");
        var current = await AuthorizeAsync(sessionId, token, ct);
        var bytes = await ReadBoundedAsync(input, length.Value, ct);
        for (var offset = 0; offset < bytes.Length; offset += 188)
            if (bytes[offset] != 0x47 || (bytes[offset + 1] & 0x80) != 0 || (bytes[offset + 3] & 0x30) == 0) throw new HomeException("invalidPayload", "Video transport stream is invalid.");
        var digest = Convert.ToHexString(SHA256.HashData(bytes));
        var now = clock.GetUtcNow();
        VideoSegmentRow row;
        await using (var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct))
        {
            var session = await db.VideoSessions.SingleOrDefaultAsync(x => x.SessionId == sessionId && x.Active, ct);
            if (session is null || session.ExpiresAtUtc <= now || session.ProducerTokenHash != current.ProducerTokenHash) throw new HomeException("requestExpired", "Video session ended.");
            var prior = await db.VideoSegments.SingleOrDefaultAsync(x => x.SessionId == sessionId && x.Sequence == sequence, ct);
            if (prior is not null)
            {
                if (prior.ContentHash != digest || prior.SizeBytes != bytes.Length || prior.DurationMs != durationMs) throw new HomeException("duplicateChanged", "Segment sequence was already used for different content.");
                if (prior.State == "ready") { await transaction.CommitAsync(ct); return new(sequence, "ready"); }
                // A timed-out upload may still be completing. Do not race a live attempt,
                // but allow a bounded retry after it becomes stale or explicitly failed.
                if (prior.State == "uploading" && prior.CreatedAtUtc > now.AddSeconds(-30)) throw new HomeException("pendingCommand", "The video segment upload is still in progress.");
                if (session.TotalBytes + bytes.Length > MaximumSessionBytes) throw new HomeException("invalidPayload", "Video session storage limit exceeded.");
                row = prior;
                row.State = "uploading";
                row.CreatedAtUtc = now;
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            else
            {
                if (sequence != session.LastSequence + 1) throw new HomeException("conflict", "Video segments must be uploaded in order.");
                if (session.TotalBytes + bytes.Length > MaximumSessionBytes) throw new HomeException("invalidPayload", "Video session storage limit exceeded.");
                row = new VideoSegmentRow { SessionId = sessionId, Sequence = sequence, ContentHash = digest, SizeBytes = bytes.Length, DurationMs = durationMs, State = "uploading", BlobName = $"{session.HouseId:D}/{sessionId:D}/{sequence:D6}.ts", CreatedAtUtc = now };
                db.VideoSegments.Add(row);
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
        }
        try
        {
            await AuthorizeAsync(sessionId, token, ct);
            using var uploadTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            uploadTimeout.CancelAfter(TimeSpan.FromSeconds(30));
            // Adopt a blob left by an earlier ambiguous attempt instead of overwriting it.
            var etag = await storage.FindAsync(row, bytes, uploadTimeout.Token);
            if (etag is null)
            {
                try { etag = await storage.CreateAsync(row, bytes, uploadTimeout.Token); }
                catch (RequestFailedException error) when (error.Status is 409 or 412)
                {
                    etag = await storage.FindAsync(row, bytes, uploadTimeout.Token);
                    if (etag is null) throw new HomeException("duplicateChanged", "A different object already uses this segment name.");
                }
            }
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var session = await db.VideoSessions.SingleOrDefaultAsync(x => x.SessionId == sessionId && x.Active, ct);
            var segment = await db.VideoSegments.SingleOrDefaultAsync(x => x.SessionId == sessionId && x.Sequence == sequence, ct);
            if (session is null || session.ExpiresAtUtc <= clock.GetUtcNow() || segment is null || segment.State != "uploading") throw new HomeException("requestExpired", "Video session ended before the segment was confirmed.");
            if (segment.ContentHash != digest) throw new HomeException("duplicateChanged", "Segment content changed during confirmation.");
            if (session.LastSequence != sequence - 1) throw new HomeException("conflict", "Video segments must be confirmed in order.");
            segment.BlobETag = etag; segment.State = "ready"; session.LastSequence = sequence; session.TotalBytes += bytes.Length;
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
            return new(sequence, "ready");
        }
        catch
        {
            try { await db.VideoSegments.Where(x => x.SessionId == sessionId && x.Sequence == sequence && x.State != "ready").ExecuteUpdateAsync(u => u.SetProperty(x => x.State, "failed"), CancellationToken.None); } catch { }
            throw;
        }
    }

    public async Task PublishPlaylistAsync(Guid sessionId, string token, IReadOnlyList<long>? sequences, CancellationToken ct)
    {
        if (sequences is null || sequences.Count is < 1 or > 12 || sequences.Any(x => x is < 0 or >= MaximumSegments) || sequences.Zip(sequences.Skip(1), (a, b) => b == a + 1).Any(x => !x)) throw new HomeException("invalidPayload", "Video playlist is invalid.");
        await AuthorizeAsync(sessionId, token, ct);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var session = await db.VideoSessions.SingleOrDefaultAsync(x => x.SessionId == sessionId && x.Active, ct);
        if (session is null || session.ExpiresAtUtc <= clock.GetUtcNow()) throw new HomeException("requestExpired", "Video session ended.");
        var prior = session.PlaylistJson is null ? [] : JsonSerializer.Deserialize<long[]>(session.PlaylistJson) ?? [];
        if (prior.Length > 0 && (sequences[^1] < prior[^1] || sequences[0] < prior[0] || sequences[^1] == prior[^1] && !prior.SequenceEqual(sequences))) throw new HomeException("conflict", "Video playlist cannot move backwards or refresh without a new segment.");
        var requested = sequences.ToArray();
        var rows = await db.VideoSegments.AsNoTracking().Where(x => x.SessionId == sessionId && requested.Contains(x.Sequence) && x.State == "ready").ToListAsync(ct);
        if (rows.Count != requested.Length) throw new HomeException("invalidPayload", "Video playlist references an unavailable segment.");
        if (!prior.SequenceEqual(requested))
        {
            session.PlaylistJson = JsonSerializer.Serialize(requested); session.PlaylistUpdatedAtUtc = clock.GetUtcNow();
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
    }

    private async Task<VideoSessionRow> AuthorizeAsync(Guid id, string token, CancellationToken ct)
    {
        if (id == Guid.Empty || token.Length is < 40 or > 64) throw new HomeException("unauthorized", "Video producer is not authorized.");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        var row = await db.VideoSessions.AsNoTracking().SingleOrDefaultAsync(x => x.SessionId == id, ct);
        if (row?.ProducerTokenHash is null || !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(row.ProducerTokenHash), Encoding.ASCII.GetBytes(hash))) throw new HomeException("unauthorized", "Video producer is not authorized.");
        if (!row.Active || row.ExpiresAtUtc <= clock.GetUtcNow()) throw new HomeException("requestExpired", "Video session ended.");
        var command = await db.Commands.AsNoTracking().SingleOrDefaultAsync(x => x.CommandId == row.CommandId && x.HouseId == row.HouseId && x.DeviceId == row.DeviceId && x.Type == "StartVideo", ct);
        if (command?.DispatchedAtUtc is null || command.Status is not ("pending" or "unknown" or "executed")) throw new HomeException("unauthorized", "Video command is not eligible.");
        if (!await authorizer.CheckAsync(new(row.UserId, row.UserSessionId), row.HouseId, "home:control:capture", command.CorrelationId, ct)) throw new HomeException("forbidden", "Video permission was revoked.");
        return row;
    }
    private static async Task<byte[]> ReadBoundedAsync(Stream input, long length, CancellationToken ct)
    {
        using var buffer = new MemoryStream((int)length); var chunk = new byte[64 * 1024]; long total = 0;
        while (true)
        {
            var read = await input.ReadAsync(chunk, ct); if (read == 0) break;
            total += read; if (total > length || total > MaximumSegmentBytes) throw new HomeException("invalidPayload", "Video segment exceeds its declared size.");
            await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
        }
        if (total != length) throw new HomeException("invalidPayload", "Video segment is incomplete.");
        return buffer.ToArray();
    }
}
