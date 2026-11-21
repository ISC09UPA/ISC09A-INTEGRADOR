using System.Data;
using System.Security.Cryptography;
using System.Text;
using CasaDomotica.Notifications.Application;
using CasaDomotica.Notifications.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CasaDomotica.Notifications.Infrastructure;

public sealed class SqlNotificationsStore(NotificationsDb db) : INotificationsStore
{
    public async Task<NotificationPreferencesDto> GetPreferencesAsync(Guid houseId, CancellationToken ct)
    {
        var row = await db.Preferences.AsNoTracking().SingleOrDefaultAsync(x => x.HouseId == houseId, ct);
        return ToPreferencesDto(row);
    }

    public async Task<NotificationPreferencesDto> SavePreferencesAsync(Guid houseId, bool emailEnabled, DateTimeOffset now, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                var row = await db.Preferences.SingleOrDefaultAsync(x => x.HouseId == houseId, ct);
                if (emailEnabled && string.IsNullOrWhiteSpace(row?.VerifiedEmail))
                    throw new NotificationException("recipientNotVerified", "Verify a recipient email before enabling email alerts.", 422);

                if (row is null)
                {
                    row = new NotificationPreferenceRow { HouseId = houseId };
                    db.Preferences.Add(row);
                }

                row.EmailEnabled = emailEnabled;
                row.UpdatedAtUtc = now;
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return ToPreferencesDto(row);
            }
            catch (Exception ex) when (attempt < 4 && IsDeadlock(ex))
            {
                db.ChangeTracker.Clear();
                await Task.Delay(15, ct);
            }
        }

        throw new InvalidOperationException("Could not save notification preferences.");
    }

    public async Task<(DeliveryWorkItem? EmailDelivery, VerificationChallengeDto Challenge)> CreateVerificationChallengeAsync(
        Guid houseId,
        string email,
        string codeHash,
        DateTimeOffset expiresAtUtc,
        string verificationSubject,
        string verificationBody,
        Guid correlationId,
        bool dryRun,
        DateTimeOffset now,
        CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var row = await db.Preferences.SingleOrDefaultAsync(x => x.HouseId == houseId, ct);
        if (row is null)
        {
            row = new NotificationPreferenceRow { HouseId = houseId };
            db.Preferences.Add(row);
        }

        row.PendingRecipientEmail = email;
        row.VerificationCodeHash = codeHash;
        row.VerificationExpiresAtUtc = expiresAtUtc;
        row.UpdatedAtUtc = now;

        NotificationDeliveryRow? deliveryRow = null;
        if (!dryRun)
        {
            deliveryRow = new NotificationDeliveryRow
            {
                DeliveryId = Guid.NewGuid(),
                HouseId = houseId,
                Kind = "verification",
                SourceEventId = null,
                IdempotencyKey = $"verify:{Guid.NewGuid():N}",
                RecipientEmail = email,
                Subject = verificationSubject,
                BodyText = verificationBody,
                Status = DeliveryStatuses.Requested,
                CorrelationId = correlationId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            db.Deliveries.Add(deliveryRow);
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var challenge = new VerificationChallengeDto(
            email,
            dryRun ? "dryRun" : DeliveryStatuses.Requested,
            expiresAtUtc,
            deliveryRow?.DeliveryId);
        return (deliveryRow is null ? null : ToWorkItem(deliveryRow), challenge);
    }

    public async Task<NotificationPreferencesDto> ConfirmVerificationCodeAsync(Guid houseId, string codeHash, DateTimeOffset now, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var row = await db.Preferences.SingleOrDefaultAsync(x => x.HouseId == houseId, ct);
        if (row is null || string.IsNullOrWhiteSpace(row.PendingRecipientEmail) ||
            string.IsNullOrWhiteSpace(row.VerificationCodeHash) || row.VerificationExpiresAtUtc is null)
            throw new NotificationException("verificationNotFound", "No pending recipient verification exists.", 404);

        if (row.VerificationExpiresAtUtc <= now)
            throw new NotificationException("verificationExpired", "Verification code expired.", 422);

        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(row.VerificationCodeHash), Encoding.ASCII.GetBytes(codeHash)))
            throw new NotificationException("invalidVerificationCode", "Verification code does not match.", 422);

        row.VerifiedEmail = row.PendingRecipientEmail;
        row.VerifiedAtUtc = now;
        row.PendingRecipientEmail = null;
        row.VerificationCodeHash = null;
        row.VerificationExpiresAtUtc = null;
        row.UpdatedAtUtc = now;

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToPreferencesDto(row);
    }

    public async Task<(DeliveryWorkItem Work, bool NewlyCreated)> CreateOrGetDeliveryAsync(
        Guid houseId,
        string kind,
        Guid? sourceEventId,
        string idempotencyKey,
        string subject,
        string bodyText,
        Guid correlationId,
        DateTimeOffset now,
        CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var prefs = await db.Preferences.AsNoTracking().SingleOrDefaultAsync(x => x.HouseId == houseId, ct);
        if (string.IsNullOrWhiteSpace(prefs?.VerifiedEmail))
            throw new NotificationException("recipientNotVerified", "No verified recipient email is configured for this house.", 422);

        var existing = await db.Deliveries.AsNoTracking().SingleOrDefaultAsync(x => x.HouseId == houseId && x.IdempotencyKey == idempotencyKey, ct);
        if (existing is not null)
        {
            await tx.CommitAsync(ct);
            return (ToWorkItem(existing), false);
        }

        var row = new NotificationDeliveryRow
        {
            DeliveryId = Guid.NewGuid(),
            HouseId = houseId,
            Kind = kind,
            SourceEventId = sourceEventId,
            IdempotencyKey = idempotencyKey,
            RecipientEmail = prefs.VerifiedEmail,
            Subject = subject,
            BodyText = bodyText,
            Status = DeliveryStatuses.Requested,
            CorrelationId = correlationId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        db.Deliveries.Add(row);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return (ToWorkItem(row), true);
    }

    public async Task<DeliveryWorkItem?> TryClaimForSendAsync(Guid deliveryId, DateTimeOffset now, CancellationToken ct)
    {
        var updated = await db.Deliveries
            .Where(x => x.DeliveryId == deliveryId && x.Status == DeliveryStatuses.Requested)
            .ExecuteUpdateAsync(u => u
                .SetProperty(x => x.Status, DeliveryStatuses.Unknown)
                .SetProperty(x => x.ErrorCode, "dispatchInFlight")
                .SetProperty(x => x.UpdatedAtUtc, now), ct);
        if (updated != 1) return null;

        var row = await db.Deliveries.AsNoTracking().SingleAsync(x => x.DeliveryId == deliveryId, ct);
        return ToWorkItem(row);
    }

    public async Task<DeliveryDto> CompleteSendAttemptAsync(Guid deliveryId, EmailDispatchResult result, DateTimeOffset now, CancellationToken ct)
    {
        await db.Deliveries
            .Where(x => x.DeliveryId == deliveryId)
            .ExecuteUpdateAsync(u => u
                .SetProperty(x => x.Status, result.Status)
                .SetProperty(x => x.ProviderOperationId, result.OperationId)
                .SetProperty(x => x.ErrorCode, result.ErrorCode)
                .SetProperty(x => x.UpdatedAtUtc, now), ct);

        var row = await db.Deliveries.AsNoTracking().SingleAsync(x => x.DeliveryId == deliveryId, ct);
        return ToDeliveryDto(row);
    }

    public async Task<DeliveryDto?> GetDeliveryAsync(Guid houseId, Guid deliveryId, CancellationToken ct)
    {
        var row = await db.Deliveries.AsNoTracking().SingleOrDefaultAsync(x => x.HouseId == houseId && x.DeliveryId == deliveryId, ct);
        return row is null ? null : ToDeliveryDto(row);
    }

    public async Task<EventIngestPlan> RecordIncomingEventAsync(NotificationEvent message, string bodyHash, DateTimeOffset now, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                var prior = await db.Inbox.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == message.EventId, ct);
                if (prior is not null)
                {
                    if (!string.Equals(prior.BodyHash, bodyHash, StringComparison.Ordinal))
                        throw new InvalidDataException("Notification event identifier was reused with different content.");
                    await tx.CommitAsync(ct);
                    return new EventIngestPlan(true, null);
                }

                var prefs = await db.Preferences.AsNoTracking().SingleOrDefaultAsync(x => x.HouseId == message.HouseId, ct);
                string outcome;
                NotificationDeliveryRow? deliveryRow = null;

                if (prefs?.EmailEnabled == true && !string.IsNullOrWhiteSpace(prefs.VerifiedEmail))
                {
                    var key = $"event:{message.EventId:D}";
                    deliveryRow = new NotificationDeliveryRow
                    {
                        DeliveryId = Guid.NewGuid(),
                        HouseId = message.HouseId,
                        Kind = "event",
                        SourceEventId = message.EventId,
                        IdempotencyKey = key,
                        RecipientEmail = prefs.VerifiedEmail,
                        Subject = message.Subject,
                        BodyText = message.BodyText,
                        Status = DeliveryStatuses.Requested,
                        CorrelationId = message.CorrelationId,
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now
                    };
                    db.Deliveries.Add(deliveryRow);
                    outcome = "queued";
                }
                else
                {
                    outcome = prefs?.EmailEnabled != true ? "skippedDisabled" : "skippedUnverified";
                }

                db.Inbox.Add(new NotificationsInboxRow
                {
                    EventId = message.EventId,
                    BodyHash = bodyHash,
                    Outcome = outcome,
                    ReceivedAtUtc = now
                });

                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return new EventIngestPlan(false, deliveryRow is null ? null : ToWorkItem(deliveryRow));
            }
            catch (Exception ex) when (attempt < 4 && IsDeadlock(ex))
            {
                db.ChangeTracker.Clear();
                await Task.Delay(15, ct);
            }
        }

        throw new InvalidOperationException("Could not record notification event.");
    }

    private static NotificationPreferencesDto ToPreferencesDto(NotificationPreferenceRow? row) => new(
        row?.EmailEnabled ?? false,
        row?.VerifiedEmail,
        row?.VerifiedAtUtc,
        row?.PendingRecipientEmail,
        row?.UpdatedAtUtc);

    private static DeliveryWorkItem ToWorkItem(NotificationDeliveryRow row) => new(
        row.DeliveryId,
        row.HouseId,
        row.Kind,
        row.SourceEventId,
        row.IdempotencyKey,
        row.RecipientEmail,
        row.Subject,
        row.BodyText,
        row.Status,
        row.ProviderOperationId,
        row.ErrorCode,
        row.CorrelationId,
        row.CreatedAtUtc,
        row.UpdatedAtUtc);

    private static DeliveryDto ToDeliveryDto(NotificationDeliveryRow row) => new(
        row.DeliveryId,
        row.Kind,
        row.Status,
        row.ProviderOperationId,
        row.ErrorCode,
        row.CreatedAtUtc,
        row.UpdatedAtUtc);

    private static bool IsDeadlock(Exception ex)
    {
        for (var cur = ex; cur is not null; cur = cur.InnerException)
        {
            if (cur is SqlException { Number: 1205 }) return true;
        }
        return false;
    }
}
