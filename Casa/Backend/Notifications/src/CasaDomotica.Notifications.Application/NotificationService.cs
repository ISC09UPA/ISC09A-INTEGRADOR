using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using CasaDomotica.Notifications.Domain;

namespace CasaDomotica.Notifications.Application;

public sealed class NotificationException(string code, string message, int statusCode = 400) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}

public sealed record NotificationPreferencesDto(
    bool Email,
    string? VerifiedEmail,
    DateTimeOffset? VerifiedAtUtc,
    string? PendingRecipientEmail,
    DateTimeOffset? UpdatedAtUtc);

public sealed record DeliveryDto(
    Guid Id,
    string Kind,
    string Status,
    string? ProviderOperationId,
    string? ErrorCode,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record VerificationChallengeDto(
    string PendingRecipientEmail,
    string Status,
    DateTimeOffset ExpiresAtUtc,
    Guid? DeliveryId);

public sealed record DeliveryWorkItem(
    Guid DeliveryId,
    Guid HouseId,
    string Kind,
    Guid? SourceEventId,
    string IdempotencyKey,
    string RecipientEmail,
    string Subject,
    string BodyText,
    string Status,
    string? ProviderOperationId,
    string? ErrorCode,
    Guid CorrelationId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record EmailDispatchResult(string Status, string? OperationId = null, string? ErrorCode = null);

public sealed record EventIngestPlan(bool Duplicate, DeliveryWorkItem? PendingSend);

public interface INotificationsAuthorizer
{
    Task<bool> CheckAsync(Guid userId, Guid sessionId, Guid houseId, string action, Guid correlationId, CancellationToken ct);
}

public interface IEmailProvider
{
    Task<EmailDispatchResult> SendAsync(DeliveryWorkItem delivery, CancellationToken ct);
    Task<EmailDispatchResult?> CheckOperationAsync(string operationId, CancellationToken ct);
    Task VerifyConfigurationAsync(CancellationToken ct);
}

public interface INotificationsStore
{
    Task<NotificationPreferencesDto> GetPreferencesAsync(Guid houseId, CancellationToken ct);
    Task<NotificationPreferencesDto> SavePreferencesAsync(Guid houseId, bool emailEnabled, DateTimeOffset now, CancellationToken ct);
    Task<(DeliveryWorkItem? EmailDelivery, VerificationChallengeDto Challenge)> CreateVerificationChallengeAsync(
        Guid houseId,
        string email,
        string codeHash,
        DateTimeOffset expiresAtUtc,
        string verificationSubject,
        string verificationBody,
        Guid correlationId,
        bool dryRun,
        DateTimeOffset now,
        CancellationToken ct);
    Task<NotificationPreferencesDto> ConfirmVerificationCodeAsync(Guid houseId, string codeHash, DateTimeOffset now, CancellationToken ct);
    Task<(DeliveryWorkItem Work, bool NewlyCreated)> CreateOrGetDeliveryAsync(
        Guid houseId,
        string kind,
        Guid? sourceEventId,
        string idempotencyKey,
        string subject,
        string bodyText,
        Guid correlationId,
        DateTimeOffset now,
        CancellationToken ct);
    Task<DeliveryWorkItem?> TryClaimForSendAsync(Guid deliveryId, DateTimeOffset now, CancellationToken ct);
    Task<DeliveryDto> CompleteSendAttemptAsync(Guid deliveryId, EmailDispatchResult result, DateTimeOffset now, CancellationToken ct);
    Task<DeliveryDto?> GetDeliveryAsync(Guid houseId, Guid deliveryId, CancellationToken ct);
    Task<EventIngestPlan> RecordIncomingEventAsync(NotificationEvent message, string bodyHash, DateTimeOffset now, CancellationToken ct);
}

public sealed class NotificationOperations(INotificationsStore store, IEmailProvider provider, TimeProvider clock)
{
    public static string NormalizeEmail(string? raw)
    {
        var trimmed = (raw ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Length > 254 || !MailAddress.TryCreate(trimmed, out var parsed) || parsed.Address != trimmed)
            throw new NotificationException("badRequest", "Invalid email address format.", 400);
        return trimmed;
    }

    public static string HashSecret(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

    public Task<NotificationPreferencesDto> GetPreferencesAsync(Guid houseId, CancellationToken ct) =>
        store.GetPreferencesAsync(houseId, ct);

    public Task<NotificationPreferencesDto> SavePreferencesAsync(Guid houseId, bool emailEnabled, CancellationToken ct) =>
        store.SavePreferencesAsync(houseId, emailEnabled, clock.GetUtcNow(), ct);

    public async Task<VerificationChallengeDto> RequestRecipientVerificationAsync(
        Guid houseId,
        string? rawEmail,
        Guid correlationId,
        bool dryRun,
        CancellationToken ct)
    {
        var email = NormalizeEmail(rawEmail);
        var code = Convert.ToInt32(RandomNumberGenerator.GetInt32(100000, 1000000)).ToString("D6");
        var codeHash = HashSecret(code);
        var now = clock.GetUtcNow();
        var expiresAt = now.AddMinutes(15);
        var subject = "Código de verificación — Casa Domótica";
        var body = $"Tu código de verificación es {code}. Caduca en 15 minutos. Si no solicitaste este aviso, puedes ignorar este mensaje.";

        var (delivery, challenge) = await store.CreateVerificationChallengeAsync(
            houseId, email, codeHash, expiresAt, subject, body, correlationId, dryRun, now, ct);

        if (delivery is null || dryRun)
            return challenge;

        var completed = await DispatchSafeAsync(delivery, dryRun: false, ct);
        return challenge with { Status = completed.Status, DeliveryId = completed.Id };
    }

    public Task<NotificationPreferencesDto> ConfirmRecipientVerificationAsync(Guid houseId, string? rawCode, CancellationToken ct)
    {
        var trimmed = (rawCode ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Length is < 6 or > 64)
            throw new NotificationException("badRequest", "Verification code format is invalid.", 400);

        return store.ConfirmVerificationCodeAsync(houseId, HashSecret(trimmed), clock.GetUtcNow(), ct);
    }

    public async Task<DeliveryDto> RequestTestEmailAsync(
        Guid houseId,
        string? idempotencyKey,
        Guid correlationId,
        bool dryRun,
        CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var effectiveKey = string.IsNullOrWhiteSpace(idempotencyKey)
            ? $"test:{Guid.NewGuid():N}"
            : $"test:{idempotencyKey.Trim()}";
        if (effectiveKey.Length > 128)
            throw new NotificationException("badRequest", "Idempotency key is too long.", 400);

        var (work, _) = await store.CreateOrGetDeliveryAsync(
            houseId,
            kind: "test",
            sourceEventId: null,
            idempotencyKey: effectiveKey,
            subject: "Correo de prueba — Casa Domótica",
            bodyText: $"Prueba de notificaciones solicitada manualmente el {now:yyyy-MM-dd HH:mm:ss} UTC.",
            correlationId: correlationId,
            now: now,
            ct: ct);

        if (!DeliveryStatuses.CanAttemptSend(work.Status))
            return ToDto(work);

        return await DispatchSafeAsync(work, dryRun, ct);
    }

    public async Task<DeliveryDto?> GetDeliveryAsync(Guid houseId, Guid deliveryId, CancellationToken ct)
    {
        var existing = await store.GetDeliveryAsync(houseId, deliveryId, ct);
        if (existing is null) return null;

        if (existing.Status == DeliveryStatuses.Accepted && !string.IsNullOrWhiteSpace(existing.ProviderOperationId))
        {
            var refreshed = await provider.CheckOperationAsync(existing.ProviderOperationId, ct);
            if (refreshed is not null && refreshed.Status != existing.Status)
                return await store.CompleteSendAttemptAsync(deliveryId, refreshed, clock.GetUtcNow(), ct);
        }

        return existing;
    }

    public async Task ApplyDeliveryReportAsync(Guid houseId, Guid deliveryId, string status, string? errorCode, CancellationToken ct)
    {
        if (status is not (DeliveryStatuses.Delivered or DeliveryStatuses.Failed))
            throw new NotificationException("badRequest", "Unsupported delivery report status.", 400);

        var existing = await store.GetDeliveryAsync(houseId, deliveryId, ct)
            ?? throw new NotificationException("notFound", "Delivery record not found.", 404);

        await store.CompleteSendAttemptAsync(
            deliveryId,
            new EmailDispatchResult(status, existing.ProviderOperationId, errorCode),
            clock.GetUtcNow(),
            ct);
    }

    public async Task IngestEventAsync(string? messageId, string body, CancellationToken ct)
    {
        var parsed = NotificationProjection.Parse(body);
        if (!string.IsNullOrWhiteSpace(messageId) &&
            !string.Equals(parsed.EventId.ToString(), messageId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Message identifier mismatch.");

        var bodyHash = HashSecret(body);
        var plan = await store.RecordIncomingEventAsync(parsed, bodyHash, clock.GetUtcNow(), ct);
        if (plan.Duplicate || plan.PendingSend is null)
            return;

        if (!DeliveryStatuses.CanAttemptSend(plan.PendingSend.Status))
            return;

        await DispatchSafeAsync(plan.PendingSend, dryRun: false, ct);
    }

    private async Task<DeliveryDto> DispatchSafeAsync(DeliveryWorkItem work, bool dryRun, CancellationToken ct)
    {
        if (dryRun)
        {
            return await store.CompleteSendAttemptAsync(
                work.DeliveryId,
                new EmailDispatchResult(DeliveryStatuses.Requested, null, "dryRun"),
                clock.GetUtcNow(),
                ct);
        }

        var claimed = await store.TryClaimForSendAsync(work.DeliveryId, clock.GetUtcNow(), ct);
        if (claimed is null)
        {
            var current = await store.GetDeliveryAsync(work.HouseId, work.DeliveryId, ct);
            return current ?? ToDto(work);
        }

        EmailDispatchResult result;
        try
        {
            result = await provider.SendAsync(claimed, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await store.CompleteSendAttemptAsync(
                work.DeliveryId,
                new EmailDispatchResult(DeliveryStatuses.Unknown, null, "dispatchCancelled"),
                clock.GetUtcNow(),
                CancellationToken.None);
            throw;
        }
        catch (Exception)
        {
            // Transport failure or timeout after send initiation leaves delivery outcome ambiguous ("unknown")
            // so retries will never trigger a duplicate email.
            result = new EmailDispatchResult(DeliveryStatuses.Unknown, null, "providerAmbiguous");
        }

        return await store.CompleteSendAttemptAsync(work.DeliveryId, result, clock.GetUtcNow(), ct);
    }

    private static DeliveryDto ToDto(DeliveryWorkItem work) => new(
        work.DeliveryId,
        work.Kind,
        work.Status,
        work.ProviderOperationId,
        work.ErrorCode,
        work.CreatedAtUtc,
        work.UpdatedAtUtc);
}
