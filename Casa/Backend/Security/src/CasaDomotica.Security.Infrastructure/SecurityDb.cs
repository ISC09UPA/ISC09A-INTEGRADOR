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
    public DbSet<OutboxRow> Outbox => Set<OutboxRow>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<InboxRow>().ToTable("SecurityInbox").HasKey(x => x.EventId);
        model.Entity<ModeRow>().ToTable("SecurityModes").HasKey(x => x.HouseId);
        model.Entity<ActivityRow>().ToTable("SecurityActivity").HasKey(x => x.EventId);
        model.Entity<IncidentRow>().ToTable("SecurityIncidents").HasKey(x => x.EventId);
        model.Entity<CaptureRow>().ToTable("SecurityCaptures").HasKey(x => x.CaptureId);
        model.Entity<CapturePolicyRow>().ToTable("SecurityCapturePolicies").HasKey(x => x.HouseId);
        model.Entity<OutboxRow>().ToTable("SecurityOutbox").HasKey(x => x.EventId);
    }
}
public sealed class InboxRow { public Guid EventId { get; set; } public string BodyHash { get; set; } = ""; public DateTimeOffset ReceivedAtUtc { get; set; } }
public sealed class ModeRow { public Guid HouseId { get; set; } public string Mode { get; set; } = ""; public Guid EventId { get; set; } public DateTimeOffset OccurredAtUtc { get; set; } public DateTimeOffset ReceivedAtUtc { get; set; } }
public sealed class ActivityRow { public Guid EventId { get; set; } public Guid HouseId { get; set; } public string Type { get; set; } = ""; public string Result { get; set; } = ""; public string? RoomId { get; set; } public DateTimeOffset OccurredAtUtc { get; set; } public DateTimeOffset ReceivedAtUtc { get; set; } }
public sealed class IncidentRow { public Guid EventId { get; set; } public Guid HouseId { get; set; } public string RoomId { get; set; } = ""; public DateTimeOffset OccurredAtUtc { get; set; } public DateTimeOffset ReceivedAtUtc { get; set; } }
public sealed class CaptureRow { public Guid CaptureId { get; set; } public Guid HouseId { get; set; } public string DeviceId { get; set; } = ""; public string BlobName { get; set; } = ""; public string BlobETag { get; set; } = ""; public DateTimeOffset OccurredAtUtc { get; set; } public DateTimeOffset ReceivedAtUtc { get; set; } public string? AnalysisStatus { get; set; } public string? AnalysisText { get; set; } public DateTimeOffset? AnalysisRequestedAtUtc { get; set; } public DateTimeOffset? AnalysisStartedAtUtc { get; set; } public string? AnalysisError { get; set; } public DateTimeOffset? DeleteRequestedAtUtc { get; set; } public DateTimeOffset? DeletedAtUtc { get; set; } public string? DeleteError { get; set; } }
public sealed class CapturePolicyRow { public Guid HouseId { get; set; } public int? RetentionDays { get; set; } public DateTimeOffset UpdatedAtUtc { get; set; } }
public sealed class OutboxRow { public Guid EventId { get; set; } public string Type { get; set; } = ""; public string BodyJson { get; set; } = ""; public DateTimeOffset CreatedAtUtc { get; set; } public DateTimeOffset? PublishedAtUtc { get; set; } }
