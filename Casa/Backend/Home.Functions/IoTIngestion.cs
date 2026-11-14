using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Azure.Identity;
using Azure.Messaging.EventHubs;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CasaDomotica.Home.Application.Commands;
using CasaDomotica.Home.Domain.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace CasaDomotica.Home.Functions;
public sealed class HomeIngestionClient(HttpClient client, IConfiguration configuration)
{
    public async Task<HttpStatusCode> SendAsync(string deviceId, JsonElement envelope, CancellationToken ct)
    {
        var root = configuration["Home:BaseUrl"]?.TrimEnd('/') ?? throw new InvalidOperationException("Configure Home:BaseUrl.");
        if (!Uri.TryCreate(root, UriKind.Absolute, out var endpoint) || endpoint.Scheme != "https") throw new InvalidOperationException("Home ingestion requires a trusted HTTPS endpoint.");
        using var ec = ECDsa.Create(); ec.ImportFromPem(configuration["Ingestion:PrivateKeyPem"] ?? throw new InvalidOperationException("Configure the protected ingestion key."));
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var assertion = new JwtSecurityToken(new JwtHeader(new SigningCredentials(new ECDsaSecurityKey(ec) { KeyId = "home-functions-v1" }, SecurityAlgorithms.EcdsaSha256)), new JwtPayload { ["iss"] = "service:home-functions", ["aud"] = "Home.Internal", ["sub"] = "service:home-functions", ["scope"] = "home:ingest", ["jti"] = Guid.NewGuid().ToString(), ["iat"] = now, ["exp"] = now + 60 });
        using var request = new HttpRequestMessage(HttpMethod.Post, root + "/api/v1/internal/device-events");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(assertion));
        request.Content = JsonContent.Create(new { sourceDeviceId = deviceId, envelope });
        using var response = await client.SendAsync(request, ct);
        if (response.IsSuccessStatusCode || response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound) return response.StatusCode;
        throw new HttpRequestException("Home ingestion is unavailable; the event must be retried.", null, response.StatusCode);
    }
}
public sealed class QuarantineStore
{
    private readonly BlobContainerClient _container;
    public QuarantineStore(IConfiguration configuration)
    {
        var root = configuration["Storage:AccountUrl"] ?? throw new InvalidOperationException("Configure private quarantine storage.");
        _container = new BlobServiceClient(new Uri(root), new DefaultAzureCredential()).GetBlobContainerClient("iot-quarantine");
    }
    public async Task KeepAsync(EventData message, string? deviceId, string reason, CancellationToken ct)
    {
        var name = $"{DateTimeOffset.UtcNow:yyyy/MM/dd}/{Guid.NewGuid():N}.json";
        var body = JsonSerializer.SerializeToUtf8Bytes(new { deviceId, reason, sequenceNumber = message.SequenceNumber, enqueuedAtUtc = message.EnqueuedTime, recordedAtUtc = DateTimeOffset.UtcNow, rawBodyBase64 = Convert.ToBase64String(message.EventBody.ToArray()) });
        using var stream = new MemoryStream(body, false);
        await _container.GetBlobClient(name).UploadAsync(stream, new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" } }, ct);
    }
}
public sealed class IoTIngestion(HomeIngestionClient home, QuarantineStore quarantine, ILogger<IoTIngestion> logger)
{
    [Function("IngestIoTEvents")]
    [FixedDelayRetry(-1, "00:00:10")]
    public async Task Run([EventHubTrigger("%IoTHubEventHubName%", Connection = "IoTHubEvents", ConsumerGroup = "%IoTConsumerGroup%")] EventData[] messages, CancellationToken ct)
    {
        foreach (var message in messages)
        {
            var deviceId = message.SystemProperties.TryGetValue("iothub-connection-device-id", out var identity) ? identity as string : null;
            if (string.IsNullOrWhiteSpace(deviceId)) { await quarantine.KeepAsync(message, null, "missingAuthenticatedDeviceMetadata", ct); continue; }
            JsonElement envelope;
            try
            {
                using var document = JsonDocument.Parse(message.EventBody.ToMemory());
                _ = DeviceEvents.Parse(document.RootElement);
                envelope = document.RootElement.Clone();
            }
            catch (Exception error) when (error is JsonException or HomeException)
            {
                await quarantine.KeepAsync(message, deviceId, "invalidDeviceEvent", ct);
                logger.LogWarning("An invalid IoT event was retained in private quarantine. Sequence: {Sequence}", message.SequenceNumber);
                continue;
            }
            var status = await home.SendAsync(deviceId, envelope, ct);
            if (status is HttpStatusCode.BadRequest or HttpStatusCode.NotFound)
            {
                await quarantine.KeepAsync(message, deviceId, "homeRejectedEvent", ct);
                logger.LogWarning("A rejected IoT event was retained in private quarantine. Sequence: {Sequence}", message.SequenceNumber);
            }
            // Other failures throw before completing the batch; the retry policy does not checkpoint lost events.
        }
    }
}
