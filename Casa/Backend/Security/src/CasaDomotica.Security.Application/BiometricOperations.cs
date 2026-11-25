using CasaDomotica.Security.Domain;

namespace CasaDomotica.Security.Application;

public sealed class BiometricException(string code, string message, int statusCode = 400) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}

public sealed record PersonDto(
    Guid Id,
    string Name,
    string Method,
    string Status,
    DateTimeOffset ValidUntilUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? RevokedAtUtc);

public sealed record EnrollmentDto(
    Guid Id,
    Guid PersonId,
    string Method,
    string Status,
    string? Instruction,
    string? Error,
    string? ErrorCode,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record PersonEnrollmentCreatedDto(PersonDto Person, EnrollmentDto Enrollment);

public sealed record BiometricVerifyResultDto(bool Allowed, string Reason, double? Confidence = null);

public sealed record FaceCapabilityResult(bool Available, string Code, string Detail);

public sealed record FaceEnrollResult(bool Succeeded, string? ReferenceId, double? Confidence, string? ErrorCode, string? ErrorMessage);

public sealed record FaceVerifyResult(bool Matched, double? Confidence, string? ErrorCode, string? ErrorMessage);

public sealed record CaptureImageData(byte[] Bytes, string ContentType);

public sealed record StoredPerson(
    Guid PersonId,
    Guid HouseId,
    string Name,
    string Method,
    string Status,
    DateTimeOffset ValidUntilUtc,
    string? BiometricReferenceId,
    Guid? EnrollmentCaptureId,
    double? ConfidenceScore,
    DateTimeOffset? RevokedAtUtc,
    DateTimeOffset? RetentionPurgedAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record StoredEnrollment(
    Guid EnrollmentId,
    Guid HouseId,
    Guid PersonId,
    string Method,
    string Status,
    string? Instruction,
    string? ErrorCode,
    string? ErrorMessage,
    DateTimeOffset ExpiresAtUtc,
    Guid? CaptureId,
    string? TemplateSlotId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public interface IFaceBiometricProvider
{
    Task<FaceCapabilityResult> CheckAccessAsync(CancellationToken ct);
    Task<FaceEnrollResult> EnrollFromImageAsync(Guid houseId, Guid personId, byte[] imageBytes, string contentType, CancellationToken ct);
    Task<FaceVerifyResult> VerifyImageAsync(Guid houseId, string referenceId, byte[] imageBytes, string contentType, CancellationToken ct);
    Task DeletePersonAsync(Guid houseId, string referenceId, CancellationToken ct);
}

public interface IBiometricStore
{
    Task<(IReadOnlyList<StoredPerson> Items, string? NextCursor)> ListPersonsAsync(Guid houseId, DateTimeOffset? cursorTime, Guid? cursorId, CancellationToken ct);
    Task<StoredPerson?> GetPersonAsync(Guid houseId, Guid personId, CancellationToken ct);
    Task<(StoredPerson Person, StoredEnrollment Session)> CreatePersonAndSessionAsync(
        Guid houseId,
        string name,
        string method,
        DateTimeOffset validUntilUtc,
        string initialInstruction,
        DateTimeOffset expiresAtUtc,
        DateTimeOffset now,
        CancellationToken ct);
    Task<StoredEnrollment?> GetEnrollmentAsync(Guid houseId, Guid enrollmentId, DateTimeOffset now, CancellationToken ct);
    Task<StoredEnrollment?> SetEnrollmentAcquiringAsync(Guid houseId, Guid enrollmentId, string instruction, DateTimeOffset now, CancellationToken ct);
    Task<StoredEnrollment?> CompleteEnrollmentAsync(
        Guid houseId,
        Guid enrollmentId,
        string? biometricReferenceId,
        Guid? captureId,
        string? templateSlotId,
        double confidence,
        DateTimeOffset now,
        CancellationToken ct);
    Task<StoredEnrollment?> FailEnrollmentAsync(Guid houseId, Guid enrollmentId, string errorCode, string errorMessage, DateTimeOffset now, CancellationToken ct);
    Task<StoredEnrollment?> CancelEnrollmentAsync(Guid houseId, Guid enrollmentId, DateTimeOffset now, CancellationToken ct);
    Task<(StoredPerson? Person, string? PreviousReferenceId)> RevokePersonAsync(Guid houseId, Guid personId, DateTimeOffset now, CancellationToken ct);
    Task<CaptureImageData?> ReadHouseCaptureAsync(Guid houseId, Guid captureId, CancellationToken ct);
    Task<IReadOnlyList<(Guid HouseId, string ReferenceId)>> PurgeExpiredAsync(DateTimeOffset now, CancellationToken ct);
}

public sealed class BiometricOperations(IBiometricStore store, IFaceBiometricProvider faceProvider, TimeProvider clock)
{
    public async Task<(IReadOnlyList<PersonDto> Items, string? NextCursor)> ListPersonsAsync(
        Guid houseId,
        DateTimeOffset? cursorTime,
        Guid? cursorId,
        CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var (rows, next) = await store.ListPersonsAsync(houseId, cursorTime, cursorId, ct);
        return (rows.Select(x => ToPersonDto(x, now)).ToList(), next);
    }

    public async Task<PersonDto?> GetPersonAsync(Guid houseId, Guid personId, CancellationToken ct)
    {
        var row = await store.GetPersonAsync(houseId, personId, ct);
        return row is null ? null : ToPersonDto(row, clock.GetUtcNow());
    }

    public async Task<PersonEnrollmentCreatedDto> StartEnrollmentAsync(
        Guid houseId,
        string? rawName,
        string? method,
        DateTimeOffset? validUntilUtc,
        CancellationToken ct)
    {
        var name = (rawName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
            throw new BiometricException("badRequest", "Provide a valid person name (1-120 characters).", 400);

        if (!BiometricMethods.IsValid(method))
            throw new BiometricException("badRequest", "Method must be 'face' or 'fingerprint'.", 400);

        var now = clock.GetUtcNow();
        if (validUntilUtc is null || validUntilUtc.Value <= now || validUntilUtc.Value > now.AddYears(5))
            throw new BiometricException("badRequest", "Validity expiration must be in the future (up to 5 years).", 400);

        var instruction = method == BiometricMethods.Face
            ? "Verifica la disponibilidad de reconocimiento facial y proporciona una captura privada de rostro."
            : "Registra la plantilla en el lector físico de huella de la casa (Etapa 11).";

        var (person, session) = await store.CreatePersonAndSessionAsync(
            houseId,
            name,
            method!,
            validUntilUtc.Value.ToUniversalTime(),
            instruction,
            now.AddMinutes(15),
            now,
            ct);

        return new(ToPersonDto(person, now), ToEnrollmentDto(session));
    }

    public async Task<EnrollmentDto?> GetEnrollmentAsync(Guid houseId, Guid enrollmentId, CancellationToken ct)
    {
        var row = await store.GetEnrollmentAsync(houseId, enrollmentId, clock.GetUtcNow(), ct);
        return row is null ? null : ToEnrollmentDto(row);
    }

    public async Task<EnrollmentDto> CaptureEnrollmentAsync(
        Guid houseId,
        Guid enrollmentId,
        Guid? captureId,
        string? templateSlotId,
        double? confidence,
        CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var current = await store.GetEnrollmentAsync(houseId, enrollmentId, now, ct)
            ?? throw new BiometricException("notFound", "Enrollment session not found.", 404);

        if (EnrollmentStatuses.IsTerminal(current.Status))
            return ToEnrollmentDto(current);

        if (current.Method == BiometricMethods.Face)
        {
            var cap = await faceProvider.CheckAccessAsync(ct);
            if (!cap.Available)
            {
                var failed = await store.FailEnrollmentAsync(houseId, enrollmentId, cap.Code, cap.Detail, now, ct) ?? current;
                return ToEnrollmentDto(failed);
            }

            if (captureId is null || captureId == Guid.Empty)
            {
                var acquiring = await store.SetEnrollmentAcquiringAsync(
                    houseId,
                    enrollmentId,
                    "Esperando captura privada del rostro asociada a la casa.",
                    now,
                    ct) ?? current;
                return ToEnrollmentDto(acquiring);
            }

            var image = await store.ReadHouseCaptureAsync(houseId, captureId.Value, ct)
                ?? throw new BiometricException("notFound", "Capture was not found for this house.", 404);

            var enrolled = await faceProvider.EnrollFromImageAsync(houseId, current.PersonId, image.Bytes, image.ContentType, ct);
            if (!enrolled.Succeeded || string.IsNullOrWhiteSpace(enrolled.ReferenceId))
            {
                var failed = await store.FailEnrollmentAsync(
                    houseId,
                    enrollmentId,
                    enrolled.ErrorCode ?? "faceProviderFailed",
                    enrolled.ErrorMessage ?? "Face biometric provider failed to enroll.",
                    now,
                    ct) ?? current;
                return ToEnrollmentDto(failed);
            }

            if (enrolled.Confidence is null || enrolled.Confidence.Value < BiometricPolicy.MinimumConfidence)
            {
                await faceProvider.DeletePersonAsync(houseId, enrolled.ReferenceId, CancellationToken.None);
                var failed = await store.FailEnrollmentAsync(
                    houseId,
                    enrollmentId,
                    "insufficientConfidence",
                    "Face enrollment confidence is below the minimum threshold.",
                    now,
                    ct) ?? current;
                return ToEnrollmentDto(failed);
            }

            var completed = await store.CompleteEnrollmentAsync(
                houseId,
                enrollmentId,
                enrolled.ReferenceId,
                captureId,
                null,
                enrolled.Confidence.Value,
                now,
                ct) ?? current;
            return ToEnrollmentDto(completed);
        }

        // Fingerprint enrollment contract (physical reader only; never phone biometrics).
        var slot = templateSlotId?.Trim();
        if (string.IsNullOrWhiteSpace(slot))
        {
            var acquiring = await store.SetEnrollmentAcquiringAsync(
                houseId,
                enrollmentId,
                "Esperando lectura en el lector físico de huella de la casa (Etapa 11).",
                now,
                ct) ?? current;
            return ToEnrollmentDto(acquiring);
        }

        if (slot.Length > 64)
            throw new BiometricException("badRequest", "Fingerprint template identifier is invalid.", 400);

        if (confidence is null || confidence.Value < BiometricPolicy.MinimumConfidence || confidence.Value > 1.0)
        {
            var failed = await store.FailEnrollmentAsync(
                houseId,
                enrollmentId,
                "insufficientConfidence",
                "Fingerprint capture quality is below the required threshold.",
                now,
                ct) ?? current;
            return ToEnrollmentDto(failed);
        }

        var completedFinger = await store.CompleteEnrollmentAsync(
            houseId,
            enrollmentId,
            slot,
            null,
            slot,
            confidence.Value,
            now,
            ct) ?? current;
        return ToEnrollmentDto(completedFinger);
    }

    public async Task<EnrollmentDto?> CancelEnrollmentAsync(Guid houseId, Guid enrollmentId, CancellationToken ct)
    {
        var row = await store.CancelEnrollmentAsync(houseId, enrollmentId, clock.GetUtcNow(), ct);
        return row is null ? null : ToEnrollmentDto(row);
    }

    public async Task<PersonDto?> RevokePersonAsync(Guid houseId, Guid personId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var (revoked, previousRef) = await store.RevokePersonAsync(houseId, personId, now, ct);
        if (revoked is null) return null;

        if (revoked.Method == BiometricMethods.Face && !string.IsNullOrWhiteSpace(previousRef))
        {
            await faceProvider.DeletePersonAsync(houseId, previousRef, ct);
        }

        return ToPersonDto(revoked, now);
    }

    public async Task<BiometricVerifyResultDto> VerifyAccessAsync(
        Guid houseId,
        Guid personId,
        string method,
        Guid? captureId,
        string? templateSlotId,
        CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var person = await store.GetPersonAsync(houseId, personId, ct);
        if (person is null)
            return new(false, "personNotFound");

        if (person.Method != method)
            return new(false, "methodMismatch");

        if (!BiometricPolicy.IsAccessAllowed(person.Status, person.ValidUntilUtc, person.RevokedAtUtc, person.RetentionPurgedAtUtc, person.ConfidenceScore, now))
            return new(false, BiometricPolicy.EffectiveStatus(person.Status, person.ValidUntilUtc, person.RevokedAtUtc, now));

        if (string.IsNullOrWhiteSpace(person.BiometricReferenceId))
            return new(false, "biometricReferencePurged");

        if (method == BiometricMethods.Face)
        {
            var cap = await faceProvider.CheckAccessAsync(ct);
            if (!cap.Available)
                return new(false, cap.Code);

            if (captureId is null || captureId == Guid.Empty)
                return new(false, "captureRequired");

            var image = await store.ReadHouseCaptureAsync(houseId, captureId.Value, ct);
            if (image is null)
                return new(false, "captureNotFound");

            var verified = await faceProvider.VerifyImageAsync(houseId, person.BiometricReferenceId, image.Bytes, image.ContentType, ct);
            if (!verified.Matched || verified.Confidence is null || verified.Confidence.Value < BiometricPolicy.MinimumConfidence)
                return new(false, verified.ErrorCode ?? "insufficientConfidence", verified.Confidence);

            return new(true, "verified", verified.Confidence);
        }

        if (string.IsNullOrWhiteSpace(templateSlotId) ||
            !string.Equals(templateSlotId.Trim(), person.BiometricReferenceId, StringComparison.Ordinal))
            return new(false, "fingerprintMismatch");

        return new(true, "verified", person.ConfidenceScore);
    }

    public Task<FaceCapabilityResult> CheckFaceAvailabilityAsync(CancellationToken ct) =>
        faceProvider.CheckAccessAsync(ct);

    private static PersonDto ToPersonDto(StoredPerson row, DateTimeOffset now) =>
        new(
            row.PersonId,
            row.Name,
            row.Method,
            BiometricPolicy.EffectiveStatus(row.Status, row.ValidUntilUtc, row.RevokedAtUtc, now),
            row.ValidUntilUtc,
            row.CreatedAtUtc,
            row.UpdatedAtUtc,
            row.RevokedAtUtc);

    private static EnrollmentDto ToEnrollmentDto(StoredEnrollment row) =>
        new(
            row.EnrollmentId,
            row.PersonId,
            row.Method,
            row.Status,
            row.Instruction,
            row.ErrorMessage,
            row.ErrorCode,
            row.ExpiresAtUtc,
            row.UpdatedAtUtc);
}