using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CasaDomotica.Security.Infrastructure;

public interface IImageAnalyzer
{
    Task<string> AnalyzeAsync(byte[] image, CancellationToken ct);
}
public sealed class AzureVisionAnalyzer(HttpClient client, IConfiguration configuration) : IImageAnalyzer
{
    public async Task<string> AnalyzeAsync(byte[] image, CancellationToken ct)
    {
        var root = configuration["Vision:Endpoint"]?.TrimEnd('/');
        var key = configuration["Vision:ApiKey"];
        if (!Uri.TryCreate(root, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) || string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Vision is not configured.");
        using var request = new HttpRequestMessage(HttpMethod.Post, root + "/computervision/imageanalysis:analyze?api-version=2024-02-01&features=caption&language=en");
        request.Headers.TryAddWithoutValidation("Ocp-Apim-Subscription-Key", key);
        request.Content = new ByteArrayContent(image); request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength is > 65536) throw new InvalidOperationException("Image analysis provider failed.");
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream(); var chunk = new byte[8192];
        while (true) { var read = await stream.ReadAsync(chunk, ct); if (read == 0) break; if (buffer.Length + read > 65536) throw new InvalidOperationException("Image analysis response is too large."); await buffer.WriteAsync(chunk.AsMemory(0, read), ct); }
        using var body = JsonDocument.Parse(buffer.ToArray());
        if (!body.RootElement.TryGetProperty("captionResult", out var caption) || !caption.TryGetProperty("text", out var text) || text.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(text.GetString()) || text.GetString()!.Length > 2000) throw new InvalidOperationException("Image analysis response is invalid.");
        return text.GetString()!;
    }
}

public sealed class CaptureAnalysis(SecurityDb db, ICaptureContent content, IImageAnalyzer analyzer, TimeProvider clock)
{
    public async Task RunOnceAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var started = await db.Captures.AsNoTracking().Where(x => x.AnalysisStatus == "analyzing" && x.DeleteRequestedAtUtc == null).Take(20).ToListAsync(ct);
        foreach (var row in started.Where(x => x.AnalysisStartedAtUtc is not null && x.AnalysisStartedAtUtc < now.AddMinutes(-5)))
            await db.Captures.Where(x => x.CaptureId == row.CaptureId && x.AnalysisStatus == "analyzing" && x.AnalysisStartedAtUtc == row.AnalysisStartedAtUtc).ExecuteUpdateAsync(update => update.SetProperty(x => x.AnalysisStatus, "failed").SetProperty(x => x.AnalysisError, "interrupted"), ct);
        var job = await db.Captures.AsNoTracking().Where(x => x.AnalysisStatus == "pending" && x.DeleteRequestedAtUtc == null).FirstOrDefaultAsync(ct);
        if (job is null) return;
        var claimed = await db.Captures.Where(x => x.CaptureId == job.CaptureId && x.AnalysisStatus == "pending" && x.DeleteRequestedAtUtc == null).ExecuteUpdateAsync(update => update.SetProperty(x => x.AnalysisStatus, "analyzing").SetProperty(x => x.AnalysisStartedAtUtc, now), ct);
        if (claimed != 1) return;
        try
        {
            var image = await content.ReadAsync(job, ct) ?? throw new InvalidDataException("Capture unavailable.");
            if (!await db.Captures.AsNoTracking().AnyAsync(x => x.CaptureId == job.CaptureId && x.AnalysisStatus == "analyzing" && x.AnalysisStartedAtUtc == now && x.DeleteRequestedAtUtc == null, ct)) return;
            var description = await analyzer.AnalyzeAsync(image.Bytes, ct);
            await db.Captures.Where(x => x.CaptureId == job.CaptureId && x.AnalysisStatus == "analyzing" && x.AnalysisStartedAtUtc == now && x.DeleteRequestedAtUtc == null).ExecuteUpdateAsync(update => update.SetProperty(x => x.AnalysisStatus, "completed").SetProperty(x => x.AnalysisText, description).SetProperty(x => x.AnalysisError, (string?)null), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception error)
        {
            var code = error is InvalidDataException ? "captureUnavailable" : "providerUnavailable";
            await db.Captures.Where(x => x.CaptureId == job.CaptureId && x.AnalysisStatus == "analyzing" && x.AnalysisStartedAtUtc == now && x.DeleteRequestedAtUtc == null).ExecuteUpdateAsync(update => update.SetProperty(x => x.AnalysisStatus, "failed").SetProperty(x => x.AnalysisError, code), ct);
        }
    }
}

public sealed class CaptureAnalysisWorker(IServiceScopeFactory scopes, ILogger<CaptureAnalysisWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await using var scope = scopes.CreateAsyncScope(); await scope.ServiceProvider.GetRequiredService<CaptureAnalysis>().RunOnceAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogWarning("Capture analysis unavailable: {Type}", error.GetType().Name); }
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
}
