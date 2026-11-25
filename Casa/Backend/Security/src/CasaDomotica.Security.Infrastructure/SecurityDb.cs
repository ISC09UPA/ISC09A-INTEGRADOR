using Microsoft.EntityFrameworkCore;

namespace CasaDomotica.Security.Infrastructure;

public sealed class SecurityDb(DbContextOptions<SecurityDb> options) : DbContext(options)
{
    public DbSet<InboxRow> Inbox => Set<InboxRow>();
    public DbSet<ModeRow> Modes => Set<ModeRow>();
    public DbSet<ActivityRow> Activity => Set<ActivityRow>();
    public DbSet<IncidentRow> Incidents => Set<IncidentRow>();
    public DbSet<CaptureRow> Captures => Set<CaptureRow>();
    public DbSet<CapturePolicyRow> CapturePolicies => Set<CapturePolicyRow>();
    public DbSet<PreferenceRow> Preferences => Set<PreferenceRow>();
    public DbSet<OutboxRow> Outbox => Set<OutboxRow>();
    public DbSet<PersonRow> Persons => Set<PersonRow>();
    public DbSet<EnrollmentSessionRow> EnrollmentSessions => Set<EnrollmentSessionRow>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<InboxRow>().ToTable("SecurityInbox").HasKey(x => x.EventId);
        model.Entity<ModeRow>().ToTable("SecurityModes").HasKey(x => x.HouseId);
        model.Entity<ActivityRow>().ToTable("SecurityActivity").HasKey(x => x.EventId);
        model.Entity<IncidentRow>().ToTable("SecurityIncidents").HasKey(x => x.EventId);
        model.Entity<CaptureRow>().ToTable("SecurityCaptures").HasKey(x => x.CaptureId);
        model.Entity<CapturePolicyRow>().ToTable("SecurityCapturePolicies").HasKey(x => x.HouseId);
        model.Entity<PreferenceRow>().ToTable("SecurityPreferences").HasKey(x => x.HouseId);
        model.Entity<OutboxRow>().ToTable("SecurityOutbox").HasKey(x => x.EventId);
        model.Entity<PersonRow>().ToTable("SecurityPersons").HasKey(x => x.PersonId);
        model.Entity<EnrollmentSessionRow>(e =>
        {
            e.ToTable("SecurityEnrollmentSessions");
            e.HasKey(x => x.EnrollmentId);
            e.HasOne<PersonRow>().WithMany().HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
public sealed class InboxRow { public Guid EventId { get; set; } public string BodyHash { get; set; } = ""; public DateTimeOffset ReceivedAtUtc { get; set; } }
public sealed class ModeRow { public Guid HouseId { get; set; } public string Mode { get; set; } = ""; public Guid EventId { get; set; } public DateTimeOffset OccurredAtUtc { get; set; } public DateTimeOffset ReceivedAtUtc { get; set; } }
public sealed class ActivityRow { public Guid EventId { get; set; } public Guid HouseId { get; set; } public string Type { get; set; } = ""; public string Result { get; set; } = ""; public string? RoomId { get; set; } public DateTimeOffset OccurredAtUtc { get; set; } public DateTimeOffset ReceivedAtUtc { get; set; } }
public sealed class IncidentRow { public Guid EventId { get; set; } public Guid HouseId { get; set; } public string RoomId { get; set; } = ""; public DateTimeOffset OccurredAtUtc { get; set; } public DateTimeOffset ReceivedAtUtc { get; set; } }
public sealed class CaptureRow { public Guid CaptureId { get; set; } public Guid HouseId { get; set; } public string DeviceId { get; set; } = ""; public string BlobName { get; set; } = ""; public string BlobETag { get; set; } = ""; public DateTimeOffset OccurredAtUtc { get; set; } public DateTimeOffset ReceivedAtUtc { get; set; } public string? AnalysisStatus { get; set; } public string? AnalysisText { get; set; } public DateTimeOffset? AnalysisRequestedAtUtc { get; set; } public DateTimeOffset? AnalysisStartedAtUtc { get; set; } public string? AnalysisError { get; set; } public DateTimeOffset? DeleteRequestedAtUtc { get; set; } public DateTimeOffset? DeletedAtUtc { get; set; } public string? DeleteError { get; set; } }
public sealed class CapturePolicyRow { public Guid HouseId { get; set; } public int? RetentionDays { get; set; } public DateTimeOffset UpdatedAtUtc { get; set; } }
public sealed class PreferenceRow { public Guid HouseId { get; set; } public bool CapturesEnabled { get; set; } public bool AnalysisEnabled { get; set; } public DateTimeOffset UpdatedAtUtc { get; set; } }
public sealed class OutboxRow { public Guid EventId { get; set; } public string Type { get; set; } = ""; public string BodyJson { get; set; } = ""; public DateTimeOffset CreatedAtUtc { get; set; } public DateTimeOffset? PublishedAtUtc { get; set; } }
public sealed class PersonRow
{
    public Guid PersonId { get; set; }
    public Guid HouseId { get; set; }
    public string Name { get; set; } = "";
    public string Method { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTimeOffset ValidUntilUtc { get; set; }
    public string? BiometricReferenceId { get; set; }
    public Guid? EnrollmentCaptureId { get; set; }
    public double? ConfidenceScore { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public DateTimeOffset? RetentionPurgedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
public sealed class EnrollmentSessionRow
{
    public Guid EnrollmentId { get; set; }
    public Guid HouseId { get; set; }
    public Guid PersonId { get; set; }
    public string Method { get; set; } = "";
    public string Status { get; set; } = "";
    public string? Instruction { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public Guid? CaptureId { get; set; }
    public string? TemplateSlotId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
