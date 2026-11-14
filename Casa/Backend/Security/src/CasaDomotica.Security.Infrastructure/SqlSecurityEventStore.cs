using System.Data;
using System.Text.Json;
using CasaDomotica.Security.Application;
using CasaDomotica.Security.Domain;
using Microsoft.EntityFrameworkCore;

namespace CasaDomotica.Security.Infrastructure;

public sealed class SqlSecurityEventStore(SecurityDb db, TimeProvider clock) : ISecurityEventStore
{
    public async Task StoreAsync(HouseEvent message, ProjectionChange change, string bodyHash, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var prior = await db.Inbox.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == message.EventId, ct);
        if (prior is not null)
        {
            if (prior.BodyHash != bodyHash) throw new InvalidDataException("Event identifier was reused with different content.");
            await transaction.CommitAsync(ct);
            return;
        }
        db.Inbox.Add(new InboxRow { EventId = message.EventId, BodyHash = bodyHash, ReceivedAtUtc = clock.GetUtcNow() });
        if (change.Mode is { } observation)
        {
            var current = await db.Modes.SingleOrDefaultAsync(x => x.HouseId == message.HouseId, ct);
            if (current is null)
            {
                current = new ModeRow { HouseId = message.HouseId };
                db.Modes.Add(current);
            }
            if (current.EventId == Guid.Empty || SecurityProjection.IsNewer(observation, new(current.Mode, current.OccurredAtUtc, current.ReceivedAtUtc, current.EventId)))
            {
                current.EventId = observation.EventId; current.Mode = observation.Mode;
                current.OccurredAtUtc = observation.OccurredAtUtc; current.ReceivedAtUtc = observation.ReceivedAtUtc;
            }
        }
        if (change.Activity is { } activity)
        {
            db.Activity.Add(new ActivityRow { EventId = activity.EventId, HouseId = message.HouseId, Type = activity.Type, Result = activity.Result, RoomId = activity.RoomId, OccurredAtUtc = activity.OccurredAtUtc, ReceivedAtUtc = activity.ReceivedAtUtc });
            if (activity.Type == "access")
            {
                var outputId = Guid.NewGuid();
                var body = JsonSerializer.Serialize(new { schemaVersion = 1, type = "AccessRecorded", eventId = outputId, correlationId = message.CorrelationId, houseId = message.HouseId, occurredAtUtc = activity.OccurredAtUtc, receivedAtUtc = activity.ReceivedAtUtc, payload = new { activityId = activity.EventId, result = activity.Result, roomId = activity.RoomId } });
                db.Outbox.Add(new OutboxRow { EventId = outputId, Type = "AccessRecorded", BodyJson = body, CreatedAtUtc = clock.GetUtcNow() });
            }
        }
        if (change.Incident is { } incident)
        {
            db.Incidents.Add(new IncidentRow { EventId = incident.EventId, HouseId = message.HouseId, RoomId = incident.RoomId, OccurredAtUtc = incident.OccurredAtUtc, ReceivedAtUtc = incident.ReceivedAtUtc });
            var outgoingId = Guid.NewGuid();
            var body = JsonSerializer.Serialize(new { schemaVersion = 1, type = "SecurityIncidentCreated", eventId = outgoingId, correlationId = message.CorrelationId, houseId = message.HouseId, occurredAtUtc = incident.OccurredAtUtc, receivedAtUtc = incident.ReceivedAtUtc, payload = new { incidentId = incident.EventId, roomId = incident.RoomId } });
            db.Outbox.Add(new OutboxRow { EventId = outgoingId, Type = "SecurityIncidentCreated", BodyJson = body, CreatedAtUtc = clock.GetUtcNow() });
        }
        if (change.Capture is { } capture)
        {
            var existing = await db.Captures.SingleOrDefaultAsync(x => x.CaptureId == capture.CaptureId, ct);
            if (existing is not null && (existing.HouseId != message.HouseId || existing.DeviceId != message.SourceDeviceId || existing.BlobName != capture.BlobName || existing.BlobETag != capture.BlobETag))
                throw new InvalidDataException("Capture identifier was reused for another file.");
            if (existing is null)
                db.Captures.Add(new CaptureRow { CaptureId = capture.CaptureId, HouseId = message.HouseId, DeviceId = message.SourceDeviceId, BlobName = capture.BlobName, BlobETag = capture.BlobETag, OccurredAtUtc = capture.OccurredAtUtc, ReceivedAtUtc = capture.ReceivedAtUtc });
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
