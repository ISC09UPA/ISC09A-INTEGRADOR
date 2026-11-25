using Microsoft.EntityFrameworkCore;

namespace CasaDomotica.Identity.Infrastructure.Persistence;

public sealed class IdentityDb(DbContextOptions<IdentityDb> options) : DbContext(options)
{
    public DbSet<UserRow> Users => Set<UserRow>();
    public DbSet<HouseRow> Houses => Set<HouseRow>();
    public DbSet<MembershipRow> Memberships => Set<MembershipRow>();
    public DbSet<SessionRow> Sessions => Set<SessionRow>();
    public DbSet<RefreshRow> RefreshTokens => Set<RefreshRow>();
    public DbSet<AssertionRow> Assertions => Set<AssertionRow>();
    public DbSet<InvitationRow> Invitations => Set<InvitationRow>();
    public DbSet<PasswordResetTokenRow> PasswordResetTokens => Set<PasswordResetTokenRow>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<UserRow>(e =>
        {
            e.ToTable("IdentityUsers");
            e.HasKey(x => x.Id);
            e.Property(x => x.Email).HasMaxLength(254);
            e.Property(x => x.NormalizedEmail).HasMaxLength(254);
            e.HasIndex(x => x.NormalizedEmail).IsUnique();
            e.Property(x => x.PasswordHash).HasMaxLength(512);
        });
        model.Entity<HouseRow>(e =>
        {
            e.ToTable("IdentityHouses");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200);
        });
        model.Entity<MembershipRow>(e =>
        {
            e.ToTable("IdentityMemberships");
            e.HasKey(x => new { x.UserId, x.HouseId });
            e.Property(x => x.Role).HasMaxLength(20);
            e.HasOne<UserRow>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<HouseRow>().WithMany().HasForeignKey(x => x.HouseId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<SessionRow>(e =>
        {
            e.ToTable("IdentitySessions");
            e.HasKey(x => x.Id);
            e.HasOne<UserRow>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<RefreshRow>(e =>
        {
            e.ToTable("IdentityRefreshTokens");
            e.HasKey(x => x.Hash);
            e.Property(x => x.Hash).HasMaxLength(64).IsUnicode(false);
            e.HasOne<SessionRow>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<AssertionRow>(e =>
        {
            e.ToTable("IdentityAssertions");
            e.HasKey(x => x.Jti);
            e.Property(x => x.Jti).HasMaxLength(128);
        });
        model.Entity<InvitationRow>(e =>
        {
            e.ToTable("IdentityInvitations");
            e.HasKey(x => x.Id);
            e.Property(x => x.Email).HasMaxLength(254);
            e.Property(x => x.NormalizedEmail).HasMaxLength(254);
            e.Property(x => x.Role).HasMaxLength(20);
            e.Property(x => x.TokenHash).HasMaxLength(64).IsUnicode(false);
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasOne<HouseRow>().WithMany().HasForeignKey(x => x.HouseId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<UserRow>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<PasswordResetTokenRow>(e =>
        {
            e.ToTable("IdentityPasswordResetTokens");
            e.HasKey(x => x.TokenHash);
            e.Property(x => x.TokenHash).HasMaxLength(64).IsUnicode(false);
            e.HasOne<UserRow>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}

public sealed class UserRow
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string NormalizedEmail { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public bool EmailVerified { get; set; } = true;
}

public sealed class HouseRow { public Guid Id { get; set; } public string Name { get; set; } = ""; }
public sealed class MembershipRow { public Guid UserId { get; set; } public Guid HouseId { get; set; } public string Role { get; set; } = "resident"; }
public sealed class SessionRow { public Guid Id { get; set; } public Guid UserId { get; set; } public DateTimeOffset CreatedAt { get; set; } public DateTimeOffset ExpiresAt { get; set; } public DateTimeOffset? RevokedAt { get; set; } }
public sealed class RefreshRow { public string Hash { get; set; } = ""; public Guid SessionId { get; set; } public DateTimeOffset IssuedAt { get; set; } public DateTimeOffset? UsedAt { get; set; } }
public sealed class AssertionRow { public string Jti { get; set; } = ""; public DateTimeOffset ExpiresAt { get; set; } }

public sealed class InvitationRow
{
    public Guid Id { get; set; }
    public Guid HouseId { get; set; }
    public string Email { get; set; } = "";
    public string NormalizedEmail { get; set; } = "";
    public string Role { get; set; } = "resident";
    public string TokenHash { get; set; } = "";
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? AcceptedAtUtc { get; set; }
    public Guid? AcceptedByUserId { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
}

public sealed class PasswordResetTokenRow
{
    public string TokenHash { get; set; } = "";
    public Guid UserId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? UsedAtUtc { get; set; }
}