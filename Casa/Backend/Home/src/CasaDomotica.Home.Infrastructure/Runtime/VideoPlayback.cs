using System.Globalization;
using System.Text;
using System.Text.Json;
using CasaDomotica.Home.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CasaDomotica.Home.Infrastructure.Runtime;

public sealed class VideoPlayback(HomeDb db, VideoSessions sessions, IPrivateVideoStore storage, TimeProvider clock)
{
    public async Task<string?> PlaylistAsync(UserContext user, Guid houseId, Guid sessionId, CancellationToken ct)
    {
        var session = await sessions.AuthorizedActiveAsync(user, houseId, sessionId, ct);
        if (session?.PlaylistJson is null || session.PlaylistUpdatedAtUtc is null || session.PlaylistUpdatedAtUtc < clock.GetUtcNow().AddSeconds(-20)) return null;
        var sequences = JsonSerializer.Deserialize<long[]>(session.PlaylistJson) ?? [];
        if (sequences.Length is < 1 or > 12) return null;
        var rows = await db.VideoSegments.AsNoTracking().Where(x => x.SessionId == sessionId && sequences.Contains(x.Sequence) && x.State == "ready" && x.DeletedAtUtc == null).ToListAsync(ct);
        if (rows.Count != sequences.Length) return null;
        var bySequence = rows.ToDictionary(x => x.Sequence);
        var playlist = new StringBuilder("#EXTM3U\n#EXT-X-VERSION:3\n#EXT-X-TARGETDURATION:10\n#EXT-X-MEDIA-SEQUENCE:");
        playlist.Append(sequences[0].ToString(CultureInfo.InvariantCulture)).Append('\n');
        foreach (var sequence in sequences)
        {
            var segment = bySequence[sequence];
            playlist.Append("#EXTINF:").Append((segment.DurationMs / 1000.0).ToString("0.000", CultureInfo.InvariantCulture)).Append(",\nsegments/").Append(sequence.ToString(CultureInfo.InvariantCulture)).Append(".ts\n");
        }
        return playlist.ToString();
    }
    public async Task<byte[]?> SegmentAsync(UserContext user, Guid houseId, Guid sessionId, long sequence, CancellationToken ct)
    {
        if (sequence is < 0 or >= VideoProducer.MaximumSegments) return null;
        if (await sessions.AuthorizedActiveAsync(user, houseId, sessionId, ct) is null) return null;
        var row = await db.VideoSegments.AsNoTracking().SingleOrDefaultAsync(x => x.SessionId == sessionId && x.Sequence == sequence && x.State == "ready" && x.DeletedAtUtc == null, ct);
        if (row is null) return null;
        var bytes = await storage.ReadAsync(row, ct);
        if (bytes is null || await sessions.AuthorizedActiveAsync(user, houseId, sessionId, ct) is null) return null;
        return bytes;
    }
}
