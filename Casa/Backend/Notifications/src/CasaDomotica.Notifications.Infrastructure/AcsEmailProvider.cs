using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using CasaDomotica.Notifications.Application;
using CasaDomotica.Notifications.Domain;
using Microsoft.Extensions.Configuration;

namespace CasaDomotica.Notifications.Infrastructure;

public sealed class AcsEmailProvider(HttpClient client, IConfiguration configuration) : IEmailProvider
{
    public async Task<EmailDispatchResult> SendAsync(DeliveryWorkItem delivery, CancellationToken ct)
    {
        var (endpoint, accessKey, sender) = ReadSettings();
        var requestUri = new Uri(endpoint, "emails:send?api-version=2023-03-31");
        var payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            senderAddress = sender,
            recipients = new
            {
                to = new[] { new { address = delivery.RecipientEmail } }
            },
            content = new
            {
                subject = delivery.Subject,
                plainText = delivery.BodyText
            }
        });

        using var request = await BuildSignedRequestAsync(HttpMethod.Post, requestUri, payload, accessKey, ct);
        request.Content = new ByteArrayContent(payload);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        request.Headers.TryAddWithoutValidation("repeatability-request-id", delivery.DeliveryId.ToString("D"));
        request.Headers.TryAddWithoutValidation("repeatability-first-sent", delivery.CreatedAtUtc.UtcDateTime.ToString("r", CultureInfo.InvariantCulture));

        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            using var body = await ReadBoundedJsonAsync(response, ct);

            if ((int)response.StatusCode is >= 200 and < 300)
            {
                var opId = body is not null && body.RootElement.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String
                    ? idProp.GetString()
                    : response.Headers.TryGetValues("x-ms-request-id", out var values) ? values.FirstOrDefault() : null;
                return new EmailDispatchResult(DeliveryStatuses.Accepted, opId);
            }

            // Explicit 4xx rejections (except 408 timeout / 429 throttle) confirm the provider did not admit the send.
            var statusCode = (int)response.StatusCode;
            if (statusCode is >= 400 and < 500 && statusCode is not (408 or 429))
                return new EmailDispatchResult(DeliveryStatuses.Failed, null, $"provider_{statusCode}");

            return new EmailDispatchResult(DeliveryStatuses.Unknown, null, $"provider_{statusCode}");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return new EmailDispatchResult(DeliveryStatuses.Unknown, null, "providerAmbiguous");
        }
    }

    public async Task<EmailDispatchResult?> CheckOperationAsync(string operationId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(operationId)) return null;
        var (endpoint, accessKey, _) = ReadSettings();
        var requestUri = new Uri(endpoint, $"emails/operations/{Uri.EscapeDataString(operationId)}?api-version=2023-03-31");

        try
        {
            using var request = await BuildSignedRequestAsync(HttpMethod.Get, requestUri, Array.Empty<byte>(), accessKey, ct);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode) return null;
            using var doc = await ReadBoundedJsonAsync(response, ct);
            if (doc is null || !doc.RootElement.TryGetProperty("status", out var statusProp)) return null;
            var status = statusProp.GetString();
            if (string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase))
                return new EmailDispatchResult(DeliveryStatuses.Failed, operationId, "providerFailed");
            // ACS "Succeeded" only means ACS accepted and relayed outbound mail, not guaranteed inbox delivery.
            return new EmailDispatchResult(DeliveryStatuses.Accepted, operationId);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public Task VerifyConfigurationAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _ = ReadSettings();
        return Task.CompletedTask;
    }

    private (Uri Endpoint, byte[]? AccessKey, string SenderAddress) ReadSettings()
    {
        var sender = configuration["Email:SenderAddress"]?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(sender) || !MailAddress.TryCreate(sender, out var parsed) || parsed.Address != sender)
            throw new InvalidOperationException("Email:SenderAddress is invalid or missing.");

        string? endpointRaw = configuration["Email:Endpoint"]?.Trim();
        byte[]? accessKey = null;

        var conn = configuration["Email:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(conn))
        {
            foreach (var part in conn.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var idx = part.IndexOf('=');
                if (idx <= 0) continue;
                var k = part[..idx].Trim();
                var v = part[(idx + 1)..].Trim();
                if (string.Equals(k, "endpoint", StringComparison.OrdinalIgnoreCase))
                    endpointRaw = v;
                else if (string.Equals(k, "accesskey", StringComparison.OrdinalIgnoreCase))
                    accessKey = Convert.FromBase64String(v);
            }
        }
        else if (!string.IsNullOrWhiteSpace(configuration["Email:ApiKey"]))
        {
            accessKey = Convert.FromBase64String(configuration["Email:ApiKey"]!);
        }

        if (!Uri.TryCreate(endpointRaw, UriKind.Absolute, out var endpointUri) ||
            endpointUri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(endpointUri.UserInfo))
            throw new InvalidOperationException("Email:Endpoint must be a trusted HTTPS URI.");

        var normalized = new Uri(endpointUri.GetLeftPart(UriPartial.Authority).TrimEnd('/') + "/");
        return (normalized, accessKey, sender);
    }

    private static async Task<HttpRequestMessage> BuildSignedRequestAsync(
        HttpMethod method,
        Uri requestUri,
        byte[] contentBytes,
        byte[]? accessKey,
        CancellationToken ct)
    {
        var request = new HttpRequestMessage(method, requestUri);
        if (accessKey is { Length: > 0 })
        {
            var utcNow = DateTimeOffset.UtcNow.ToString("r", CultureInfo.InvariantCulture);
            var contentHash = Convert.ToBase64String(SHA256.HashData(contentBytes));
            var host = requestUri.Authority;
            var pathAndQuery = requestUri.PathAndQuery;
            var stringToSign = $"{method.Method.ToUpperInvariant()}\n{pathAndQuery}\n{utcNow};{host};{contentHash}";
            using var hmac = new HMACSHA256(accessKey);
            var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));
            request.Headers.TryAddWithoutValidation("x-ms-date", utcNow);
            request.Headers.TryAddWithoutValidation("x-ms-content-sha256", contentHash);
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "HMAC-SHA256",
                $"SignedHeaders=x-ms-date;host;x-ms-content-sha256&Signature={signature}");
            return request;
        }

        var token = await new DefaultAzureCredential().GetTokenAsync(
            new TokenRequestContext(["https://communication.azure.com/.default"]),
            ct);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        return request;
    }

    private static async Task<JsonDocument?> ReadBoundedJsonAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.Content.Headers.ContentLength is > 65536) return null;
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        while (true)
        {
            var read = await stream.ReadAsync(chunk, ct);
            if (read == 0) break;
            if (buffer.Length + read > 65536) return null;
            await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
        }
        if (buffer.Length == 0) return null;
        try { return JsonDocument.Parse(buffer.ToArray()); }
        catch (JsonException) { return null; }
    }
}
