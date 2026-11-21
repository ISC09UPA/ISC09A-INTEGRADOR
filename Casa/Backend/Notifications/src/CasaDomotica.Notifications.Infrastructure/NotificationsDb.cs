using Microsoft.EntityFrameworkCore;

namespace CasaDomotica.Notifications.Infrastructure;

public sealed class NotificationsDb(DbContextOptions<NotificationsDb> options) : DbContext(options)
{
    public DbSet<NotificationsInboxRow> Inbox => Set<NotificationsInboxRow>();
    public DbSet<NotificationPreferenceRow> Preferences => Set<NotificationPreferenceRow>();
    public DbSet<NotificationDeliveryRow> Deliveries => Set<NotificationDeliveryRow>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<NotificationsInboxRow>().ToTable("NotificationsInbox").HasKey(x => x.EventId);
        model.Entity<NotificationPreferenceRow>().ToTable("NotificationPreferences").HasKey(x => x.HouseId);
        var delivery = model.Entity<NotificationDeliveryRow>().ToTable("NotificationDeliveries");
        delivery.HasKey(x => x.DeliveryId);
        delivery.HasIndex(x => new { x.HouseId, x.IdempotencyKey }).IsUnique();
    }
}

public sealed class NotificationsInboxRow
{
    public Guid EventId { get; set; }
    public string BodyHash { get; set; } = "";
    public string Outcome { get; set; } = "";
    public DateTimeOffset ReceivedAtUtc { get; set; }
}

public sealed class NotificationPreferenceRow
{
    public Guid HouseId { get; set; }
    public bool EmailEnabled { get; set; }
    public string? VerifiedEmail { get; set; }
    public DateTimeOffset? VerifiedAtUtc { get; set; }
    public string? PendingRecipientEmail { get; set; }
    public string? VerificationCodeHash { get; set; }
    public DateTimeOffset? VerificationExpiresAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class NotificationDeliveryRow
{
    public Guid DeliveryId { get; set; }
    public Guid HouseId { get; set; }
    public string Kind { get; set; } = "";
    public Guid? SourceEventId { get; set; }
    public string IdempotencyKey { get; set; } = "";
    public string RecipientEmail { get; set; } = "";
    public string Subject { get; set; } = "";
    public string BodyText { get; set; } = "";
    public string Status { get; set; } = "";
    public string? ProviderOperationId { get; set; }
    public string? ErrorCode { get; set; }
    public Guid CorrelationId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
