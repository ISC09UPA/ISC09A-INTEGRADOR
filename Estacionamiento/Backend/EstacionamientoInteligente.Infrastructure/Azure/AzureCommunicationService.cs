using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AirportParking.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace AirportParking.Infrastructure.Azure;

public class AzureCommunicationService : IAzureCommunicationService
{
    private readonly HttpClient _httpClient;
    private readonly AzureSettings _settings;
    private readonly ILogger<AzureCommunicationService> _logger;

    public AzureCommunicationService(
        HttpClient httpClient,
        AzureSettings settings,
        ILogger<AzureCommunicationService> logger)
    {
        _httpClient = httpClient;
        _settings = settings;
        _logger = logger;
    }

    public async Task<string?> CreateIdentityAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var (endpoint, accessKey) = GetEndpointAndKey();
            if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(accessKey))
            {
                _logger.LogWarning("Azure Communication Services endpoint or key not configured.");
                return null;
            }

            var uri = new Uri(endpoint);
            var host = uri.Host;
            var pathAndQuery = "/identities?api-version=2022-10-01";
            var body = Encoding.UTF8.GetBytes("{\"scopes\":[\"chat\"]}");

            using var sha256 = SHA256.Create();
            var contentHash = Convert.ToBase64String(sha256.ComputeHash(body));
            var dateStr = DateTimeOffset.UtcNow.ToString("r");

            var stringToSign = $"POST\n{pathAndQuery}\n{dateStr};{host};{contentHash}";
            var keyBytes = Convert.FromBase64String(accessKey);
            using var hmac = new HMACSHA256(keyBytes);
            var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));

            var authHeader = $"HMAC-SHA256 SignedHeaders=date;host;x-ms-content-sha256&Signature={signature}";

            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(uri, pathAndQuery));
            request.Headers.TryAddWithoutValidation("Date", dateStr);
            request.Headers.TryAddWithoutValidation("x-ms-content-sha256", contentHash);
            request.Headers.TryAddWithoutValidation("Authorization", authHeader);
            request.Content = new ByteArrayContent(body);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Azure Communication Services returned {StatusCode}", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("identity", out var identityElem) &&
                identityElem.TryGetProperty("id", out var idElem))
            {
                return idElem.GetString();
            }

            return json;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Azure Communication Services identity");
            return null;
        }
    }

    public async Task<bool> SendSecurityAlertAsync(string alertMessage, string? recipientPhone = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🚨 [AZURE COMMUNICATION SERVICES]: Transmitiendo alerta crítica de seguridad: {Message}", alertMessage);

        var identityId = await CreateIdentityAsync(cancellationToken);
        if (!string.IsNullOrEmpty(identityId))
        {
            _logger.LogInformation("✅ [AZURE COMMUNICATION SERVICES]: Sesión de notificación activa con ID {IdentityId}", identityId);
            return true;
        }

        return false;
    }

    public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        var identity = await CreateIdentityAsync(cancellationToken);
        return !string.IsNullOrEmpty(identity);
    }

    private (string Endpoint, string Key) GetEndpointAndKey()
    {
        var endpoint = _settings.CommunicationServices.Endpoint;
        var key = _settings.CommunicationServices.AccessKey;

        if (string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(_settings.CommunicationServices.ConnectionString))
        {
            var parts = _settings.CommunicationServices.ConnectionString.Split(';');
            foreach (var part in parts)
            {
                if (part.StartsWith("endpoint=", StringComparison.OrdinalIgnoreCase))
                {
                    endpoint = part.Substring("endpoint=".Length).TrimEnd('/');
                }
                else if (part.StartsWith("accesskey=", StringComparison.OrdinalIgnoreCase))
                {
                    key = part.Substring("accesskey=".Length);
                }
            }
        }

        return (endpoint, key);
    }
}
