using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AirportParking.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace AirportParking.Infrastructure.Azure;

public class AzureVisionService : IAzureVisionService
{
    private readonly HttpClient _httpClient;
    private readonly AzureSettings _settings;
    private readonly ILogger<AzureVisionService> _logger;

    public AzureVisionService(
        HttpClient httpClient,
        AzureSettings settings,
        ILogger<AzureVisionService> logger)
    {
        _httpClient = httpClient;
        _settings = settings;
        _logger = logger;
    }

    public async Task<string?> AnalyzePlateImageAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_settings.ComputerVision.SubscriptionKey))
            {
                _logger.LogWarning("Azure Computer Vision SubscriptionKey is not configured.");
                return null;
            }

            var endpoint = _settings.ComputerVision.Endpoint.TrimEnd('/');
            var url = $"{endpoint}/vision/v3.2/read/analyze";

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Ocp-Apim-Subscription-Key", _settings.ComputerVision.SubscriptionKey);
            request.Content = new ByteArrayContent(imageBytes);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Azure Vision Read API failed with status {StatusCode}", response.StatusCode);
                return null;
            }

            var operationLocation = response.Headers.GetValues("Operation-Location").FirstOrDefault();
            if (string.IsNullOrEmpty(operationLocation))
            {
                return null;
            }

            return await PollReadResultAsync(operationLocation, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing image with Azure Computer Vision");
            return null;
        }
    }

    public async Task<string?> AnalyzePlateFromUrlAsync(string imageUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_settings.ComputerVision.SubscriptionKey))
            {
                _logger.LogWarning("Azure Computer Vision SubscriptionKey is not configured.");
                return null;
            }

            var endpoint = _settings.ComputerVision.Endpoint.TrimEnd('/');
            var url = $"{endpoint}/vision/v3.2/read/analyze";

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Ocp-Apim-Subscription-Key", _settings.ComputerVision.SubscriptionKey);
            var payload = JsonSerializer.Serialize(new { url = imageUrl });
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Azure Vision Read API URL failed with status {StatusCode}", response.StatusCode);
                return null;
            }

            var operationLocation = response.Headers.GetValues("Operation-Location").FirstOrDefault();
            if (string.IsNullOrEmpty(operationLocation))
            {
                return null;
            }

            return await PollReadResultAsync(operationLocation, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing image URL with Azure Computer Vision");
            return null;
        }
    }

    private async Task<string?> PollReadResultAsync(string operationUrl, CancellationToken cancellationToken)
    {
        for (int i = 0; i < 6; i++)
        {
            await Task.Delay(400, cancellationToken);

            using var pollRequest = new HttpRequestMessage(HttpMethod.Get, operationUrl);
            pollRequest.Headers.Add("Ocp-Apim-Subscription-Key", _settings.ComputerVision.SubscriptionKey);

            var pollResponse = await _httpClient.SendAsync(pollRequest, cancellationToken);
            if (!pollResponse.IsSuccessStatusCode) continue;

            var json = await pollResponse.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var status = doc.RootElement.GetProperty("status").GetString();

            if (status == "succeeded")
            {
                return ExtractBestPlateCandidate(doc.RootElement);
            }
            if (status == "failed")
            {
                break;
            }
        }
        return null;
    }

    private static string? ExtractBestPlateCandidate(JsonElement root)
    {
        if (!root.TryGetProperty("analyzeResult", out var analyzeResult) ||
            !analyzeResult.TryGetProperty("readResults", out var readResults))
        {
            return null;
        }

        var candidates = new List<string>();

        foreach (var page in readResults.EnumerateArray())
        {
            if (page.TryGetProperty("lines", out var lines))
            {
                foreach (var line in lines.EnumerateArray())
                {
                    if (line.TryGetProperty("text", out var textElem))
                    {
                        var text = textElem.GetString()?.Trim().ToUpperInvariant() ?? "";
                        var clean = Regex.Replace(text, @"[^A-Z0-9-]", "");
                        if (clean.Length >= 5 && clean.Length <= 10)
                        {
                            candidates.Add(clean);
                        }
                    }
                }
            }
        }

        return candidates.FirstOrDefault();
    }

    public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_settings.ComputerVision.SubscriptionKey)) return false;

            var endpoint = _settings.ComputerVision.Endpoint.TrimEnd('/');
            var url = $"{endpoint}/vision/v3.2/read/analyze";

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Ocp-Apim-Subscription-Key", _settings.ComputerVision.SubscriptionKey);
            var payload = JsonSerializer.Serialize(new { url = "https://raw.githubusercontent.com/Azure-Samples/cognitive-services-sample-data-files/master/ComputerVision/Images/faces.jpg" });
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Accepted;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Azure Computer Vision Ping failed: {Message}", ex.Message);
            return false;
        }
    }
}
