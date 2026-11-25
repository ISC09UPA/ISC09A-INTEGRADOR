using System.Data;
using System.Text;
using System.Text.Json;
using CasaDomotica.Security.Application;
using CasaDomotica.Security.Domain;
using Microsoft.EntityFrameworkCore;

namespace CasaDomotica.Security.Infrastructure;

public sealed class SqlBiometricStore(SecurityDb db, ICaptureContent captureContent) : IBiometricStore
{
    public async Task<(IReadOnlyList<StoredPerson> Items, string? NextCursor)> ListPersonsAsync(
        Guid houseId,
        DateTimeOffset? cursorTime,
        Guid? cursorId,
        CancellationToken ct)
    {
        var all = (await db.Persons.AsNoTracking()
            .Where(x => x.HouseId == houseId)
            .ToListAsync(ct))
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.PersonId)
            .AsEnumerable();

        if (cursorTime is not null && cursorId is not null)
        {
            all = all.Where(x =>
                x.CreatedAtUtc < cursorTime.Value ||
                (x.CreatedAtUtc == cursorTime.Value && x.PersonId.CompareTo(cursorId.Value) < 0));
        }

        var page = all.Take(51).ToList();
        string? next = null;
        if (page.Count > 50)
        {
            var last = page[49];
            next = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { Time = last.CreatedAtUtc, Id = last.PersonId })));
        }

        return (page.Take(50).Select(ToStoredPerson).ToList(), next);
    }

    public async Task<StoredPerson?> GetPersonAsync(Guid houseId, Guid personId, CancellationToken ct)
    {
        var row = await db.Persons.AsNoTracking().SingleOrDefaultAsync(x => x.HouseId == houseId && x.PersonId == personId, ct);
        return row is null ? null : ToStoredPerson(row);
    }

    public async Task<(StoredPerson Person, StoredEnrollment Session)> CreatePersonAndSessionAsync(
        Guid houseId,
        string name,
        string method,
        DateTimeOffset validUntilUtc,
        string initialInstruction,
        DateTimeOffset expiresAtUtc,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var person = new PersonRow
        {
            PersonId = Guid.NewGuid(),
            HouseId = houseId,
            Name = name,
            Method = method,
            Status = PersonStatuses.Pending,
            ValidUntilUtc = validUntilUtc,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        var session = new EnrollmentSessionRow
        {
            EnrollmentId = Guid.NewGuid(),
            HouseId = houseId,
            PersonId = person.PersonId,
            Method = method,
            Status = EnrollmentStatuses.Pending,
            Instruction = initialInstruction,
            ExpiresAtUtc = expiresAtUtc,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        db.Persons.Add(person);
        db.EnrollmentSessions.Add(session);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return (ToStoredPerson(person), ToStoredEnrollment(session));
    }

    public async Task<StoredEnrollment?> GetEnrollmentAsync(Guid houseId, Guid enrollmentId, DateTimeOffset now, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var row = await db.EnrollmentSessions.SingleOrDefaultAsync(x => x.HouseId == houseId && x.EnrollmentId == enrollmentId, ct);
        if (row is null)
        {
            await tx.CommitAsync(ct);
            return null;
        }

        if (!EnrollmentStatuses.IsTerminal(row.Status) && row.ExpiresAtUtc <= now)
        {
            row.Status = EnrollmentStatuses.Failed;
            row.ErrorCode = "enrollmentExpired";
            row.ErrorMessage = "La sesión de registro biométrico venció antes de completarse.";
            row.UpdatedAtUtc = now;

            var person = await db.Persons.SingleOrDefaultAsync(x => x.HouseId == houseId && x.PersonId == row.PersonId, ct);
            if (person is not null && person.Status == PersonStatuses.Pending)
            {
                person.Status = PersonStatuses.Failed;
                person.UpdatedAtUtc = now;
            }
            await db.SaveChangesAsync(ct);
        }

        await tx.CommitAsync(ct);
        return ToStoredEnrollment(row);
    }

    public async Task<StoredEnrollment?> SetEnrollmentAcquiringAsync(Guid houseId, Guid enrollmentId, string instruction, DateTimeOffset now, CancellationToken ct)
    {
        var row = await db.EnrollmentSessions.SingleOrDefaultAsync(x => x.HouseId == houseId && x.EnrollmentId == enrollmentId, ct);
        if (row is null || EnrollmentStatuses.IsTerminal(row.Status)) return row is null ? null : ToStoredEnrollment(row);
        row.Status = EnrollmentStatuses.Acquiring;
        row.Instruction = instruction;
        row.UpdatedAtUtc = now;
        await db.SaveChangesAsync(ct);
        return ToStoredEnrollment(row);
    }

    public async Task<StoredEnrollment?> CompleteEnrollmentAsync(
        Guid houseId,
        Guid enrollmentId,
        string? biometricReferenceId,
        Guid? captureId,
        string? templateSlotId,
        double confidence,
        DateTimeOffset now,
        CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var row = await db.EnrollmentSessions.SingleOrDefaultAsync(x => x.HouseId == houseId && x.EnrollmentId == enrollmentId, ct);
        if (row is null)
        {
            await tx.CommitAsync(ct);
            return null;
        }
        if (EnrollmentStatuses.IsTerminal(row.Status))
        {
            await tx.CommitAsync(ct);
            return ToStoredEnrollment(row);
        }
        if (row.ExpiresAtUtc <= now)
        {
            row.Status = EnrollmentStatuses.Failed;
            row.ErrorCode = "enrollmentExpired";
            row.ErrorMessage = "La sesión venció antes de completar el registro.";
            row.UpdatedAtUtc = now;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return ToStoredEnrollment(row);
        }

        var person = await db.Persons.SingleAsync(x => x.HouseId == houseId && x.PersonId == row.PersonId, ct);
        if (person.RevokedAtUtc is not null || person.ValidUntilUtc <= now)
        {
            row.Status = EnrollmentStatuses.Failed;
            row.ErrorCode = "personUnavailable";
            row.ErrorMessage = "El permiso de la persona ya no está vigente o fue revocado.";
            row.UpdatedAtUtc = now;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return ToStoredEnrollment(row);
        }

        row.Status = EnrollmentStatuses.Completed;
        row.ErrorCode = null;
        row.ErrorMessage = null;
        row.CaptureId = captureId;
        row.TemplateSlotId = templateSlotId;
        row.UpdatedAtUtc = now;

        person.Status = PersonStatuses.Authorized;
        person.BiometricReferenceId = biometricReferenceId;
        person.EnrollmentCaptureId = captureId;
        person.ConfidenceScore = confidence;
        person.UpdatedAtUtc = now;

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToStoredEnrollment(row);
    }

    public async Task<StoredEnrollment?> FailEnrollmentAsync(Guid houseId, Guid enrollmentId, string errorCode, string errorMessage, DateTimeOffset now, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var row = await db.EnrollmentSessions.SingleOrDefaultAsync(x => x.HouseId == houseId && x.EnrollmentId == enrollmentId, ct);
        if (row is null)
        {
            await tx.CommitAsync(ct);
            return null;
        }
        if (!EnrollmentStatuses.IsTerminal(row.Status))
        {
            row.Status = EnrollmentStatuses.Failed;
            row.ErrorCode = errorCode;
            row.ErrorMessage = errorMessage.Length > 256 ? errorMessage[..256] : errorMessage;
            row.UpdatedAtUtc = now;

            var person = await db.Persons.SingleOrDefaultAsync(x => x.HouseId == houseId && x.PersonId == row.PersonId, ct);
            if (person is not null && person.Status == PersonStatuses.Pending)
            {
                person.Status = PersonStatuses.Failed;
                person.UpdatedAtUtc = now;
            }
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
        return ToStoredEnrollment(row);
    }

    public async Task<StoredEnrollment?> CancelEnrollmentAsync(Guid houseId, Guid enrollmentId, DateTimeOffset now, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var row = await db.EnrollmentSessions.SingleOrDefaultAsync(x => x.HouseId == houseId && x.EnrollmentId == enrollmentId, ct);
        if (row is null)
        {
            await tx.CommitAsync(ct);
            return null;
        }
        if (!EnrollmentStatuses.IsTerminal(row.Status))
        {
            row.Status = EnrollmentStatuses.Cancelled;
            row.Instruction = "Registro cancelado por el administrador.";
            row.UpdatedAtUtc = now;

            var person = await db.Persons.SingleOrDefaultAsync(x => x.HouseId == houseId && x.PersonId == row.PersonId, ct);
            if (person is not null && person.Status == PersonStatuses.Pending)
            {
                person.Status = PersonStatuses.Failed;
                person.UpdatedAtUtc = now;
            }
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
        return ToStoredEnrollment(row);
    }

    public async Task<(StoredPerson? Person, string? PreviousReferenceId)> RevokePersonAsync(Guid houseId, Guid personId, DateTimeOffset now, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var person = await db.Persons.SingleOrDefaultAsync(x => x.HouseId == houseId && x.PersonId == personId, ct);
        if (person is null)
        {
            await tx.CommitAsync(ct);
            return (null, null);
        }

        var previousRef = person.BiometricReferenceId;
        person.Status = PersonStatuses.Revoked;
        person.RevokedAtUtc ??= now;
        person.BiometricReferenceId = null;
        person.ConfidenceScore = null;
        person.EnrollmentCaptureId = null;
        person.RetentionPurgedAtUtc ??= now;
        person.UpdatedAtUtc = now;

        var openSessions = await db.EnrollmentSessions
            .Where(x => x.HouseId == houseId && x.PersonId == personId && (x.Status == EnrollmentStatuses.Pending || x.Status == EnrollmentStatuses.Acquiring))
            .ToListAsync(ct);
        foreach (var session in openSessions)
        {
            session.Status = EnrollmentStatuses.Cancelled;
            session.Instruction = "Cancelada por revocación del permiso.";
            session.UpdatedAtUtc = now;
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return (ToStoredPerson(person), previousRef);
    }

    public async Task<CaptureImageData?> ReadHouseCaptureAsync(Guid houseId, Guid captureId, CancellationToken ct)
    {
        var row = await db.Captures.AsNoTracking()
            .SingleOrDefaultAsync(x => x.HouseId == houseId && x.CaptureId == captureId && x.DeleteRequestedAtUtc == null, ct);
        if (row is null) return null;
        var data = await captureContent.ReadAsync(row, ct);
        return data is null ? null : new CaptureImageData(data.Bytes, data.ContentType);
    }

    public async Task<IReadOnlyList<(Guid HouseId, string ReferenceId)>> PurgeExpiredAsync(DateTimeOffset now, CancellationToken ct)
    {
        var candidates = (await db.Persons
            .Where(x => x.RetentionPurgedAtUtc == null && (x.Status == PersonStatuses.Authorized || x.Status == PersonStatuses.Revoked || x.Status == PersonStatuses.Failed))
            .ToListAsync(ct))
            .Where(x => x.RevokedAtUtc is not null || x.ValidUntilUtc <= now || x.Status == PersonStatuses.Failed)
            .ToList();

        var externalFaceRefs = new List<(Guid HouseId, string ReferenceId)>();
        foreach (var person in candidates)
        {
            if (person.Method == BiometricMethods.Face && !string.IsNullOrWhiteSpace(person.BiometricReferenceId))
                externalFaceRefs.Add((person.HouseId, person.BiometricReferenceId));

            if (person.Status == PersonStatuses.Authorized && person.ValidUntilUtc <= now)
                person.Status = PersonStatuses.Expired;

            person.BiometricReferenceId = null;
            person.ConfidenceScore = null;
            person.EnrollmentCaptureId = null;
            person.RetentionPurgedAtUtc = now;
            person.UpdatedAtUtc = now;
        }

        if (candidates.Count > 0)
            await db.SaveChangesAsync(ct);

        return externalFaceRefs;
    }

    private static StoredPerson ToStoredPerson(PersonRow row) => new(
        row.PersonId,
        row.HouseId,
        row.Name,
        row.Method,
        row.Status,
        row.ValidUntilUtc,
        row.BiometricReferenceId,
        row.EnrollmentCaptureId,
        row.ConfidenceScore,
        row.RevokedAtUtc,
        row.RetentionPurgedAtUtc,
        row.CreatedAtUtc,
        row.UpdatedAtUtc);

    private static StoredEnrollment ToStoredEnrollment(EnrollmentSessionRow row) => new(
        row.EnrollmentId,
        row.HouseId,
        row.PersonId,
        row.Method,
        row.Status,
        row.Instruction,
        row.ErrorCode,
        row.ErrorMessage,
        row.ExpiresAtUtc,
        row.CaptureId,
        row.TemplateSlotId,
        row.CreatedAtUtc,
        row.UpdatedAtUtc);
}