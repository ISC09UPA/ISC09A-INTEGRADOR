using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;
using AirportParking.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace AirportParking.Infrastructure.Azure;

public class AzureServiceBusService : IAzureServiceBusService
{
    private readonly HttpClient _httpClient;
    private readonly AzureSettings _settings;
    private readonly ILogger<AzureServiceBusService> _logger;

    public AzureServiceBusService(
        HttpClient httpClient,
        AzureSettings settings,
        ILogger<AzureServiceBusService> logger)
    {
        _httpClient = httpClient;
        _settings = settings;
        _logger = logger;
    }

    public async Task<bool> PublishEventAsync<T>(string eventType, T payload, CancellationToken cancellationToken = default)
    {
        try
        {
            var (sbNamespace, keyName, key) = ParseServiceBusCredentials();
            if (string.IsNullOrEmpty(sbNamespace) || string.IsNullOrEmpty(key))
            {
                _logger.LogWarning("Azure Service Bus configuration is incomplete.");
                return false;
            }

            var queue = string.IsNullOrWhiteSpace(_settings.ServiceBus.QueueName)
                ? "estacionamiento-events"
                : _settings.ServiceBus.QueueName.Trim();

            var resourceUri = $"http://{sbNamespace}.servicebus.windows.net/{queue}";
            var sasToken = GenerateSasToken(resourceUri, keyName, key, TimeSpan.FromHours(1));

            var requestUri = $"https://{sbNamespace}.servicebus.windows.net/{queue}/messages?api-version=2014-01";

            var envelope = new
            {
                EventId = Guid.NewGuid().ToString("N"),
                EventType = eventType,
                Source = "AirportParking.Core",
                Timestamp = DateTime.UtcNow,
                Payload = payload
            };

            var json = JsonSerializer.Serialize(envelope);
            using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
            request.Headers.TryAddWithoutValidation("Authorization", sasToken);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("📨 [AZURE SERVICE BUS]: Evento '{EventType}' publicado exitosamente en cola '{Queue}'.", eventType, queue);
                return true;
            }

            _logger.LogWarning("Failed to publish event to Azure Service Bus. Status: {StatusCode}", response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while publishing event {EventType} to Azure Service Bus", eventType);
            return false;
        }
    }

    public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        return await PublishEventAsync("HEARTBEAT_PING", new
        {
            Ping = "OK",
            Time = DateTime.UtcNow
        }, cancellationToken);
    }

    private (string Namespace, string KeyName, string Key) ParseServiceBusCredentials()
    {
        var sbNamespace = _settings.ServiceBus.Namespace;
        var keyName = _settings.ServiceBus.KeyName;
        var key = _settings.ServiceBus.Key;

        if (!string.IsNullOrWhiteSpace(_settings.ServiceBus.ConnectionString))
        {
            var parts = _settings.ServiceBus.ConnectionString.Split(';');
            foreach (var part in parts)
            {
                if (part.StartsWith("Endpoint=", StringComparison.OrdinalIgnoreCase))
                {
                    var uriStr = part.Substring("Endpoint=".Length);
                    var clean = uriStr.Replace("sb://", "").TrimEnd('/');
                    var idx = clean.IndexOf('.');
                    if (idx > 0)
                    {
                        sbNamespace = clean.Substring(0, idx);
                    }
                    else
                    {
                        sbNamespace = clean;
                    }
                }
                else if (part.StartsWith("SharedAccessKeyName=", StringComparison.OrdinalIgnoreCase))
                {
                    keyName = part.Substring("SharedAccessKeyName=".Length);
                }
                else if (part.StartsWith("SharedAccessKey=", StringComparison.OrdinalIgnoreCase))
                {
                    key = part.Substring("SharedAccessKey=".Length);
                }
            }
        }

        return (sbNamespace, keyName, key);
    }

    private static string GenerateSasToken(string resourceUri, string keyName, string key, TimeSpan ttl)
    {
        var sinceEpoch = DateTime.UtcNow - new DateTime(1970, 1, 1);
        var expiry = Convert.ToString((int)sinceEpoch.TotalSeconds + (int)ttl.TotalSeconds);
        var stringToSign = HttpUtility.UrlEncode(resourceUri) + "\n" + expiry;

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));

        return $"SharedAccessSignature sr={HttpUtility.UrlEncode(resourceUri)}&sig={HttpUtility.UrlEncode(signature)}&se={expiry}&skn={keyName}";
    }
}
