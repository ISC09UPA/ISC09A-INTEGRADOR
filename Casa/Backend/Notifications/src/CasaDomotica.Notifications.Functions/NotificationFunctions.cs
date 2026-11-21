using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using CasaDomotica.Notifications.Application;
using CasaDomotica.Notifications.Domain;
using CasaDomotica.Notifications.Infrastructure;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace CasaDomotica.Notifications.Functions;

public sealed record SavePreferencesRequest([property: JsonRequired] bool? Email);
public sealed record VerifyRecipientRequest([property: JsonRequired] string? Email, bool? DryRun);
public sealed record ConfirmRecipientRequest([property: JsonRequired] string? Code);
public sealed record TestEmailRequest(bool? DryRun);

public sealed class NotificationFunctions(
    NotificationsDb db,
    NotificationOperations operations,
    INotificationsAuthorizer authorizer,
    IEmailProvider emailProvider,
    IdentityKey identityKey,
    IConfiguration configuration,
    ILogger<NotificationFunctions> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Function("NotificationsHealth")]
    public Task<HttpResponseData> Health(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData req) =>
        JsonResponseAsync(req, HttpStatusCode.OK, new { status = "running" });

    [Function("NotificationsReady")]
    public async Task<HttpResponseData> Ready(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "ready")] HttpRequestData req,
        CancellationToken ct)
    {
        try
        {
            await db.Database.OpenConnectionAsync(ct);
            await db.Database.CloseConnectionAsync();
            return await JsonResponseAsync(req, HttpStatusCode.OK, new { status = "ready" });
        }
        catch
        {
            return await ProblemAsync(req, HttpStatusCode.ServiceUnavailable, "Notifications storage unavailable.", "unavailable", Guid.NewGuid());
        }
    }

    [Function("NotificationsBusDiagnostic")]
    public async Task<HttpResponseData> DiagnosticsBus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/v1/notifications/houses/{houseId:guid}/diagnostics/bus")] HttpRequestData req,
        Guid houseId,
        CancellationToken ct) =>
        await ExecuteAuthorizedAsync(req, houseId, "notifications:read", async correlationId =>
        {
            var host = configuration["ServiceBusConnection:fullyQualifiedNamespace"] ?? configuration["ServiceBus:Namespace"];
            if (string.IsNullOrWhiteSpace(host))
                return await ProblemAsync(req, HttpStatusCode.ServiceUnavailable, "Notifications event transport unavailable.", "unavailable", correlationId);
            try
            {
                await using var client = new ServiceBusClient(host, new DefaultAzureCredential(), new ServiceBusClientOptions { TransportType = ServiceBusTransportType.AmqpWebSockets });
                await using var receiver = client.CreateReceiver("house-events-v1", "notifications-v1");
                _ = await receiver.PeekMessageAsync(cancellationToken: ct);
                return await JsonResponseAsync(req, HttpStatusCode.OK, new { status = "ready" });
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                return await ProblemAsync(req, HttpStatusCode.ServiceUnavailable, "Notifications event transport unavailable.", "unavailable", correlationId);
            }
        }, ct);

    [Function("NotificationsEmailProviderDiagnostic")]
    public async Task<HttpResponseData> DiagnosticsEmailProvider(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/v1/notifications/houses/{houseId:guid}/diagnostics/email-provider")] HttpRequestData req,
        Guid houseId,
        CancellationToken ct) =>
        await ExecuteAuthorizedAsync(req, houseId, "notifications:read", async correlationId =>
        {
            try
            {
                await emailProvider.VerifyConfigurationAsync(ct);
                return await JsonResponseAsync(req, HttpStatusCode.OK, new { status = "ready" });
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                return await ProblemAsync(req, HttpStatusCode.ServiceUnavailable, "Email provider is not configured.", "unavailable", correlationId);
            }
        }, ct);

    [Function("GetNotificationPreferences")]
    public Task<HttpResponseData> GetPreferences(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/v1/notifications/houses/{houseId:guid}/preferences")] HttpRequestData req,
        Guid houseId,
        CancellationToken ct) =>
        ExecuteAuthorizedAsync(req, houseId, "notifications:read", async _ =>
        {
            var prefs = await operations.GetPreferencesAsync(houseId, ct);
            return await JsonResponseAsync(req, HttpStatusCode.OK, prefs);
        }, ct);

    [Function("SaveNotificationPreferences")]
    public Task<HttpResponseData> SavePreferences(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "api/v1/notifications/houses/{houseId:guid}/preferences")] HttpRequestData req,
        Guid houseId,
        CancellationToken ct) =>
        ExecuteAuthorizedAsync(req, houseId, "notifications:manage", async correlationId =>
        {
            var body = await ReadJsonBodyAsync<SavePreferencesRequest>(req, ct);
            if (body?.Email is null)
                return await ProblemAsync(req, HttpStatusCode.BadRequest, "Invalid preferences payload.", "badRequest", correlationId);

            var updated = await operations.SavePreferencesAsync(houseId, body.Email.Value, ct);
            return await JsonResponseAsync(req, HttpStatusCode.OK, updated);
        }, ct);

    [Function("VerifyNotificationRecipient")]
    public Task<HttpResponseData> VerifyRecipient(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/v1/notifications/houses/{houseId:guid}/recipient/verify")] HttpRequestData req,
        Guid houseId,
        CancellationToken ct) =>
        ExecuteAuthorizedAsync(req, houseId, "notifications:manage", async correlationId =>
        {
            var body = await ReadJsonBodyAsync<VerifyRecipientRequest>(req, ct);
            if (body is null)
                return await ProblemAsync(req, HttpStatusCode.BadRequest, "Invalid recipient verification payload.", "badRequest", correlationId);

            var dryRun = body.DryRun ?? ReadDryRunQueryOrHeader(req);
            var result = await operations.RequestRecipientVerificationAsync(houseId, body.Email, correlationId, dryRun, ct);
            return await JsonResponseAsync(req, HttpStatusCode.Accepted, result);
        }, ct);

    [Function("ConfirmNotificationRecipient")]
    public Task<HttpResponseData> ConfirmRecipient(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/v1/notifications/houses/{houseId:guid}/recipient/confirm")] HttpRequestData req,
        Guid houseId,
        CancellationToken ct) =>
        ExecuteAuthorizedAsync(req, houseId, "notifications:manage", async correlationId =>
        {
            var body = await ReadJsonBodyAsync<ConfirmRecipientRequest>(req, ct);
            if (body is null)
                return await ProblemAsync(req, HttpStatusCode.BadRequest, "Invalid verification confirmation payload.", "badRequest", correlationId);

            var updated = await operations.ConfirmRecipientVerificationAsync(houseId, body.Code, ct);
            return await JsonResponseAsync(req, HttpStatusCode.OK, updated);
        }, ct);

    [Function("RequestTestEmail")]
    public Task<HttpResponseData> TestEmail(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/v1/notifications/houses/{houseId:guid}/test-email")] HttpRequestData req,
        Guid houseId,
        CancellationToken ct) =>
        ExecuteAuthorizedAsync(req, houseId, "notifications:manage", async correlationId =>
        {
            var body = await ReadOptionalJsonBodyAsync<TestEmailRequest>(req, ct);
            if (body.Malformed)
                return await ProblemAsync(req, HttpStatusCode.BadRequest, "Invalid test email request.", "badRequest", correlationId);

            var idempotencyKey = req.Headers.TryGetValues("Idempotency-Key", out var keys) ? keys.FirstOrDefault() : null;
            var dryRun = body.Value?.DryRun ?? ReadDryRunQueryOrHeader(req);
            var delivery = await operations.RequestTestEmailAsync(houseId, idempotencyKey, correlationId, dryRun, ct);
            var status = delivery.Status == DeliveryStatuses.Failed ? HttpStatusCode.UnprocessableEntity : HttpStatusCode.Accepted;
            return await JsonResponseAsync(req, status, delivery);
        }, ct);

    [Function("GetNotificationDelivery")]
    public Task<HttpResponseData> GetDelivery(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/v1/notifications/houses/{houseId:guid}/deliveries/{deliveryId:guid}")] HttpRequestData req,
        Guid houseId,
        Guid deliveryId,
        CancellationToken ct) =>
        ExecuteAuthorizedAsync(req, houseId, "notifications:read", async correlationId =>
        {
            var delivery = await operations.GetDeliveryAsync(houseId, deliveryId, ct);
            if (delivery is null)
                return await ProblemAsync(req, HttpStatusCode.NotFound, "Delivery record not found.", "notFound", correlationId);
            return await JsonResponseAsync(req, HttpStatusCode.OK, delivery);
        }, ct);

    [Function("ConsumeNotificationEvents")]
    public async Task ConsumeServiceBus(
        [ServiceBusTrigger("house-events-v1", "notifications-v1", Connection = "ServiceBusConnection", AutoCompleteMessages = false)]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions actions,
        CancellationToken ct)
    {
        try
        {
            await operations.IngestEventAsync(message.MessageId, message.Body.ToString(), ct);
            await actions.CompleteMessageAsync(message, ct);
        }
        catch (InvalidDataException error)
        {
            logger.LogWarning("Invalid notification event moved to dead-letter queue: {Type}", error.GetType().Name);
            await actions.DeadLetterMessageAsync(
                message,
                deadLetterReason: "invalidNotificationEvent",
                deadLetterErrorDescription: "Event rejected by the notifications contract.",
                cancellationToken: ct);
        }
    }

    private async Task<HttpResponseData> ExecuteAuthorizedAsync(
        HttpRequestData req,
        Guid houseId,
        string action,
        Func<Guid, Task<HttpResponseData>> handler,
        CancellationToken ct)
    {
        var correlationId = ResolveCorrelationId(req);
        if (!TryValidateJwt(req, out var userId, out var sessionId))
            return await ProblemAsync(req, HttpStatusCode.Unauthorized, "Authentication required.", "unauthorized", correlationId);

        try
        {
            if (!await authorizer.CheckAsync(userId, sessionId, houseId, action, correlationId, ct))
                return await ProblemAsync(req, HttpStatusCode.Forbidden, "Not authorized for this house.", "forbidden", correlationId);

            return await handler(correlationId);
        }
        catch (NotificationsAuthorizationException authEx) when (authEx.Code == "sessionRevoked")
        {
            return await ProblemAsync(req, HttpStatusCode.Unauthorized, "Session revoked.", "sessionRevoked", correlationId);
        }
        catch (NotificationsAuthorizationException)
        {
            return await ProblemAsync(req, HttpStatusCode.ServiceUnavailable, "Authorization service unavailable.", "unavailable", correlationId);
        }
        catch (NotificationException domEx)
        {
            return await ProblemAsync(req, (HttpStatusCode)domEx.StatusCode, domEx.Message, domEx.Code, correlationId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("Notifications request failed: {Type}", ex.GetType().Name);
            return await ProblemAsync(req, HttpStatusCode.ServiceUnavailable, "Notifications service unavailable.", "unavailable", correlationId);
        }
    }

    private bool TryValidateJwt(HttpRequestData req, out Guid userId, out Guid sessionId)
    {
        userId = Guid.Empty;
        sessionId = Guid.Empty;
        if (!req.Headers.TryGetValues("Authorization", out var values)) return false;
        var raw = values.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(raw) || !raw.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return false;
        var token = raw["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(token) || token.Length > 4096) return false;

        try
        {
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            var parameters = new TokenValidationParameters
            {
                ValidIssuer = "Identity",
                ValidAudience = "Notifications.Public",
                IssuerSigningKey = identityKey.SecurityKey,
                ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256],
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                ClockSkew = TimeSpan.FromSeconds(10)
            };
            var principal = handler.ValidateToken(token, parameters, out _);
            return Guid.TryParse(principal.FindFirst("sub")?.Value, out userId) &&
                   Guid.TryParse(principal.FindFirst("sid")?.Value, out sessionId) &&
                   userId != Guid.Empty &&
                   sessionId != Guid.Empty;
        }
        catch
        {
            return false;
        }
    }

    private static Guid ResolveCorrelationId(HttpRequestData req)
    {
        if (req.Headers.TryGetValues("X-Correlation-Id", out var values) && Guid.TryParse(values.FirstOrDefault(), out var parsed) && parsed != Guid.Empty)
            return parsed;
        return Guid.NewGuid();
    }

    private static bool ReadDryRunQueryOrHeader(HttpRequestData req)
    {
        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        if (string.Equals(query["dryRun"], "true", StringComparison.OrdinalIgnoreCase)) return true;
        return req.Headers.TryGetValues("X-Notification-Dry-Run", out var vals) &&
               string.Equals(vals.FirstOrDefault(), "true", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<T?> ReadJsonBodyAsync<T>(HttpRequestData req, CancellationToken ct) where T : class
    {
        try
        {
            using var reader = new StreamReader(req.Body, Encoding.UTF8);
            var raw = await reader.ReadToEndAsync(ct);
            if (string.IsNullOrWhiteSpace(raw) || Encoding.UTF8.GetByteCount(raw) > 16 * 1024) return null;
            return JsonSerializer.Deserialize<T>(raw, JsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return null;
        }
    }

    private static async Task<(T? Value, bool Malformed)> ReadOptionalJsonBodyAsync<T>(HttpRequestData req, CancellationToken ct) where T : class
    {
        try
        {
            using var reader = new StreamReader(req.Body, Encoding.UTF8);
            var raw = await reader.ReadToEndAsync(ct);
            if (string.IsNullOrWhiteSpace(raw)) return (null, false);
            if (Encoding.UTF8.GetByteCount(raw) > 16 * 1024) return (null, true);
            return (JsonSerializer.Deserialize<T>(raw, JsonOptions), false);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return (null, true);
        }
    }

    private async Task<HttpResponseData> JsonResponseAsync(HttpRequestData req, HttpStatusCode status, object payload)
    {
        var res = req.CreateResponse(status);
        res.Headers.Add("Content-Type", "application/json; charset=utf-8");
        ApplyCorsHeaders(req, res);
        await res.WriteStringAsync(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8);
        return res;
    }

    private Task<HttpResponseData> ProblemAsync(HttpRequestData req, HttpStatusCode status, string title, string code, Guid correlationId) =>
        JsonResponseAsync(req, status, new { title, status = (int)status, code, correlationId });

    private void ApplyCorsHeaders(HttpRequestData req, HttpResponseData res)
    {
        if (!req.Headers.TryGetValues("Origin", out var origins)) return;
        var origin = origins.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(origin)) return;
        var allowed = configuration.GetSection("Cors:Origins").GetChildren().Select(x => x.Value).OfType<string>();
        if (allowed.Contains(origin, StringComparer.OrdinalIgnoreCase))
        {
            res.Headers.Add("Access-Control-Allow-Origin", origin);
            res.Headers.Add("Vary", "Origin");
        }
    }
}
