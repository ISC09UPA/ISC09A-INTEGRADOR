using CasaDomotica.Security.Application;
using CasaDomotica.Security.Domain;
using CasaDomotica.Security.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CasaDomotica.Security.Infrastructure.Tests;

public sealed class BiometricOperationsTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SecurityDb _db;
    private readonly MutableTimeProvider _clock = new(new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero));
    private readonly FakeCaptureContent _captureContent = new();
    private readonly FakeFaceProvider _face = new();
    private readonly SqlBiometricStore _store;
    private readonly BiometricOperations _ops;
    private readonly Guid _houseId = Guid.NewGuid();
    private readonly Guid _captureId = Guid.NewGuid();

    public BiometricOperationsTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<SecurityDb>().UseSqlite(_connection).Options;
        _db = new SecurityDb(options);
        _db.Database.EnsureCreated();

        _db.Captures.Add(new CaptureRow
        {
            CaptureId = _captureId,
            HouseId = _houseId,
            DeviceId = "cam-1",
            BlobName = $"{_houseId:D}/cam-1/{_captureId:D}.jpg",
            BlobETag = "etag-1",
            OccurredAtUtc = _clock.GetUtcNow().AddMinutes(-1),
            ReceivedAtUtc = _clock.GetUtcNow().AddMinutes(-1)
        });
        _db.SaveChanges();

        _store = new SqlBiometricStore(_db, _captureContent);
        _ops = new BiometricOperations(_store, _face, _clock);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Face_without_limited_access_approval_stays_blocked_and_never_authorizes_person()
    {
        _face.Available = false;
        var created = await _ops.StartEnrollmentAsync(_houseId, "Ana Pérez", "face", _clock.GetUtcNow().AddDays(30), default);
        Assert.Equal("pending", created.Person.Status);
        Assert.Equal("pending", created.Enrollment.Status);

        var captured = await _ops.CaptureEnrollmentAsync(_houseId, created.Enrollment.Id, _captureId, null, null, default);
        Assert.Equal("failed", captured.Status);
        Assert.Equal("faceAccessPendingApproval", captured.ErrorCode);

        var person = await _ops.GetPersonAsync(_houseId, created.Person.Id, default);
        Assert.Equal("failed", person!.Status);

        var verify = await _ops.VerifyAccessAsync(_houseId, created.Person.Id, "face", _captureId, null, default);
        Assert.False(verify.Allowed);
    }

    [Fact]
    public async Task Low_confidence_face_enrollment_is_rejected_and_external_reference_deleted()
    {
        _face.Available = true;
        _face.EnrollConfidence = 0.42;

        var created = await _ops.StartEnrollmentAsync(_houseId, "Carlos Ruiz", "face", _clock.GetUtcNow().AddDays(10), default);
        var captured = await _ops.CaptureEnrollmentAsync(_houseId, created.Enrollment.Id, _captureId, null, null, default);

        Assert.Equal("failed", captured.Status);
        Assert.Equal("insufficientConfidence", captured.ErrorCode);
        Assert.Single(_face.DeletedReferences);

        var verify = await _ops.VerifyAccessAsync(_houseId, created.Person.Id, "face", _captureId, null, default);
        Assert.False(verify.Allowed);
    }

    [Fact]
    public async Task Face_enrollment_expiration_and_revocation_purge_biometric_reference()
    {
        _face.Available = true;
        _face.EnrollConfidence = 0.92;
        _face.VerifyConfidence = 0.91;
        _face.VerifyMatched = true;

        var validUntil = _clock.GetUtcNow().AddDays(7);
        var created = await _ops.StartEnrollmentAsync(_houseId, "María López", "face", validUntil, default);

        // No capture yet -> acquiring.
        var acquiring = await _ops.CaptureEnrollmentAsync(_houseId, created.Enrollment.Id, null, null, null, default);
        Assert.Equal("acquiring", acquiring.Status);

        // Valid capture -> completed and authorized.
        var completed = await _ops.CaptureEnrollmentAsync(_houseId, created.Enrollment.Id, _captureId, null, null, default);
        Assert.Equal("completed", completed.Status);

        var verifiedOk = await _ops.VerifyAccessAsync(_houseId, created.Person.Id, "face", _captureId, null, default);
        Assert.True(verifiedOk.Allowed);

        // Revocation immediately blocks access and purges biometric reference.
        var revoked = await _ops.RevokePersonAsync(_houseId, created.Person.Id, default);
        Assert.Equal("revoked", revoked!.Status);
        Assert.Single(_face.DeletedReferences);

        var row = await _db.Persons.SingleAsync(x => x.PersonId == created.Person.Id);
        Assert.Null(row.BiometricReferenceId);
        Assert.NotNull(row.RetentionPurgedAtUtc);

        var verifiedAfterRevoke = await _ops.VerifyAccessAsync(_houseId, created.Person.Id, "face", _captureId, null, default);
        Assert.False(verifiedAfterRevoke.Allowed);
        Assert.Equal("revoked", verifiedAfterRevoke.Reason);
    }

    [Fact]
    public async Task Fingerprint_enrollment_cancellation_expiration_and_validity_expiry_work_correctly()
    {
        // Cancellation keeps person unauthorized.
        var cancelledFlow = await _ops.StartEnrollmentAsync(_houseId, "Invitado 1", "fingerprint", _clock.GetUtcNow().AddDays(2), default);
        var cancelled = await _ops.CancelEnrollmentAsync(_houseId, cancelledFlow.Enrollment.Id, default);
        Assert.Equal("cancelled", cancelled!.Status);
        Assert.Equal("failed", (await _ops.GetPersonAsync(_houseId, cancelledFlow.Person.Id, default))!.Status);

        // Session expiration (15 min) marks session failed.
        var expiredSessionFlow = await _ops.StartEnrollmentAsync(_houseId, "Invitado 2", "fingerprint", _clock.GetUtcNow().AddDays(2), default);
        _clock.Advance(TimeSpan.FromMinutes(16));
        var expiredSession = await _ops.GetEnrollmentAsync(_houseId, expiredSessionFlow.Enrollment.Id, default);
        Assert.Equal("failed", expiredSession!.Status);
        Assert.Equal("enrollmentExpired", expiredSession.ErrorCode);

        // Complete valid fingerprint enrollment, verify, then expire person's validity window and purge.
        var validFlow = await _ops.StartEnrollmentAsync(_houseId, "Luis Gómez", "fingerprint", _clock.GetUtcNow().AddDays(1), default);
        var awaitingReader = await _ops.CaptureEnrollmentAsync(_houseId, validFlow.Enrollment.Id, null, null, null, default);
        Assert.Equal("acquiring", awaitingReader.Status);

        var completed = await _ops.CaptureEnrollmentAsync(_houseId, validFlow.Enrollment.Id, null, "fp-slot-01", 0.88, default);
        Assert.Equal("completed", completed.Status);
        Assert.True((await _ops.VerifyAccessAsync(_houseId, validFlow.Person.Id, "fingerprint", null, "fp-slot-01", default)).Allowed);

        _clock.Advance(TimeSpan.FromDays(2));
        var expiredVerify = await _ops.VerifyAccessAsync(_houseId, validFlow.Person.Id, "fingerprint", null, "fp-slot-01", default);
        Assert.False(expiredVerify.Allowed);
        Assert.Equal("expired", expiredVerify.Reason);

        await _store.PurgeExpiredAsync(_clock.GetUtcNow(), default);
        var purgedRow = await _db.Persons.SingleAsync(x => x.PersonId == validFlow.Person.Id);
        Assert.Equal("expired", purgedRow.Status);
        Assert.Null(purgedRow.BiometricReferenceId);
        Assert.NotNull(purgedRow.RetentionPurgedAtUtc);
    }

    private sealed class MutableTimeProvider(DateTimeOffset initial) : TimeProvider
    {
        private DateTimeOffset _now = initial;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan delta) => _now = _now.Add(delta);
    }

    private sealed class FakeCaptureContent : ICaptureContent
    {
        public Task<CaptureContent?> ReadAsync(CaptureRow capture, CancellationToken ct) =>
            Task.FromResult<CaptureContent?>(new CaptureContent([0xFF, 0xD8, 0xFF], "image/jpeg"));
        public Task<bool> DeleteAsync(CaptureRow capture, CancellationToken ct) => Task.FromResult(true);
    }

    private sealed class FakeFaceProvider : IFaceBiometricProvider
    {
        public bool Available;
        public double EnrollConfidence = 0.90;
        public double VerifyConfidence = 0.90;
        public bool VerifyMatched = true;
        public List<string> DeletedReferences { get; } = [];

        public Task<FaceCapabilityResult> CheckAccessAsync(CancellationToken ct) =>
            Task.FromResult(Available
                ? new FaceCapabilityResult(true, "ready", "OK")
                : new FaceCapabilityResult(false, "faceAccessPendingApproval", "Limited Access approval pending."));

        public Task<FaceEnrollResult> EnrollFromImageAsync(Guid houseId, Guid personId, byte[] imageBytes, string contentType, CancellationToken ct) =>
            Task.FromResult(new FaceEnrollResult(true, $"face-ref-{personId:N}", EnrollConfidence, null, null));

        public Task<FaceVerifyResult> VerifyImageAsync(Guid houseId, string referenceId, byte[] imageBytes, string contentType, CancellationToken ct) =>
            Task.FromResult(new FaceVerifyResult(VerifyMatched, VerifyConfidence, VerifyMatched ? null : "faceMismatch", null));

        public Task DeletePersonAsync(Guid houseId, string referenceId, CancellationToken ct)
        {
            DeletedReferences.Add(referenceId);
            return Task.CompletedTask;
        }
    }
}