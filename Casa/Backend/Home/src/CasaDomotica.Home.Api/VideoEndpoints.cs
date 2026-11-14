using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CasaDomotica.Home.Domain.Interfaces;
using CasaDomotica.Home.Infrastructure.Runtime;

public static class VideoEndpoints
{
    public static void MapVideoEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/v1/houses/{houseId:guid}/video/sessions").RequireAuthorization();
        routes.MapPost("/", async (Guid houseId, VideoStartRequest request, ClaimsPrincipal principal, VideoSessions sessions, RuntimeWakeups wakeups, IConfiguration config, HttpContext context, CancellationToken ct) =>
        {
            var result = await sessions.StartAsync(User(principal), houseId, request.ResourceId, request.IdempotencyKey, request.CorrelationId ?? Guid.NewGuid(), ct);
            context.Response.Headers.CacheControl = "private, no-store";
            wakeups.Commands(); return Results.Accepted(value: Response(result, houseId, config));
        });
        routes.MapGet("/by-key/{key}", async (Guid houseId, string key, ClaimsPrincipal principal, VideoSessions sessions, IConfiguration config, HttpContext context, CancellationToken ct) =>
        {
            var result = await sessions.ByKeyAsync(User(principal), houseId, key, ct);
            context.Response.Headers.CacheControl = "private, no-store";
            return result is null ? Results.NotFound() : Results.Ok(Response(result, houseId, config));
        });
        routes.MapGet("/{sessionId:guid}", async (Guid houseId, Guid sessionId, ClaimsPrincipal principal, VideoSessions sessions, IConfiguration config, HttpContext context, CancellationToken ct) =>
        {
            var result = await sessions.GetAsync(User(principal), houseId, sessionId, ct);
            context.Response.Headers.CacheControl = "private, no-store";
            return result is null ? Results.NotFound() : Results.Ok(Response(result, houseId, config));
        });
        routes.MapDelete("/{sessionId:guid}", async (Guid houseId, Guid sessionId, ClaimsPrincipal principal, VideoSessions sessions, IConfiguration config, HttpContext context, CancellationToken ct) =>
        {
            var result = await sessions.CloseAsync(User(principal), houseId, sessionId, ct);
            context.Response.Headers.CacheControl = "private, no-store";
            return result is null ? Results.NotFound() : Results.Ok(Response(result, houseId, config));
        });
        routes.MapGet("/{sessionId:guid}/playlist.m3u8", async (Guid houseId, Guid sessionId, ClaimsPrincipal principal, VideoPlayback playback, HttpContext context, CancellationToken ct) =>
        {
            var result = await playback.PlaylistAsync(User(principal), houseId, sessionId, ct);
            if (result is null) return Results.NotFound();
            context.Response.Headers.CacheControl = "private, no-store";
            return Results.Text(result, "application/vnd.apple.mpegurl");
        });
        routes.MapGet("/{sessionId:guid}/segments/{sequence:long}.ts", async (Guid houseId, Guid sessionId, long sequence, ClaimsPrincipal principal, VideoPlayback playback, HttpContext context, CancellationToken ct) =>
        {
            var result = await playback.SegmentAsync(User(principal), houseId, sessionId, sequence, ct);
            if (result is null) return Results.NotFound();
            context.Response.Headers.CacheControl = "private, no-store";
            return Results.File(result, "video/mp2t", enableRangeProcessing: false);
        });
        app.MapPost("/internal/video/{sessionId:guid}/segments/{sequence:long}", async (Guid sessionId, long sequence, HttpContext context, VideoProducer producer, CancellationToken ct) =>
        {
            var token = ProducerToken(context);
            if (context.Request.ContentType != "video/mp2t" || !context.Request.Headers.TryGetValue("X-Segment-Duration-Ms", out var duration) || duration.Count != 1 || !int.TryParse(duration[0], NumberStyles.None, CultureInfo.InvariantCulture, out var durationMs)) throw new HomeException("invalidPayload", "Video segment metadata is invalid.");
            var result = await producer.UploadSegmentAsync(sessionId, token, sequence, durationMs, context.Request.ContentLength, context.Request.Body, ct);
            return Results.Ok(result);
        });
        app.MapPost("/internal/video/{sessionId:guid}/playlist", async (Guid sessionId, HttpContext context, VideoProducer producer, CancellationToken ct) =>
        {
            var token = ProducerToken(context);
            if (context.Request.ContentType != "application/json" || context.Request.ContentLength is null or <= 0 or > 4096) throw new HomeException("invalidPayload", "Video playlist request is invalid.");
            var bytes = new byte[(int)context.Request.ContentLength.Value];
            try { await context.Request.Body.ReadExactlyAsync(bytes, ct); }
            catch (EndOfStreamException) { throw new HomeException("invalidPayload", "Video playlist request is incomplete."); }
            try
            {
                using var document = JsonDocument.Parse(bytes);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 1 || !root.TryGetProperty("segments", out var entries) || entries.ValueKind != JsonValueKind.Array || entries.GetArrayLength() > 12) throw new HomeException("invalidPayload", "Video playlist request is invalid.");
                var sequences = entries.EnumerateArray().Select(x => x.GetInt64()).ToArray();
                await producer.PublishPlaylistAsync(sessionId, token, sequences, ct);
                return Results.NoContent();
            }
            catch (Exception error) when (error is JsonException or InvalidOperationException or FormatException or OverflowException) { throw new HomeException("invalidPayload", "Video playlist request is invalid."); }
        });
    }
    private static object Response(VideoSessionView view, Guid houseId, IConfiguration config)
    {
        string? url = null;
        if (view.Status == "live")
        {
            var root = config["Video:PlaybackBaseUrl"]?.TrimEnd('/');
            if (!Uri.TryCreate(root, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)) throw new HomeException("unavailable", "Private video playback is not configured.");
            url = $"{root}/api/v1/houses/{houseId:D}/video/sessions/{view.Id:D}/playlist.m3u8";
        }
        return new { id = view.Id, status = view.Status, expiresAtUtc = view.ExpiresAtUtc, commandId = view.CommandId, streamUrl = url, streamType = url is null ? null : "hls" };
    }
    private static UserContext User(ClaimsPrincipal principal)
    {
        if (!Guid.TryParse(principal.FindFirstValue("sub"), out var user) || !Guid.TryParse(principal.FindFirstValue("sid"), out var session)) throw new HomeException("unauthorized", "Session is invalid.");
        return new(user, session);
    }
    private static string ProducerToken(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue("X-Video-Token", out var values) || values.Count != 1) throw new HomeException("unauthorized", "Video producer is not authorized.");
        return values[0] ?? "";
    }
}
public sealed record VideoStartRequest([property: System.Text.Json.Serialization.JsonRequired] string ResourceId, [property: System.Text.Json.Serialization.JsonRequired] string IdempotencyKey, Guid? CorrelationId);
