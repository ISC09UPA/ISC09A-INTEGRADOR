using System.Net.Mail;
using CasaDomotica.Identity.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
namespace CasaDomotica.Identity.Infrastructure.Persistence;
public sealed class IdentityAdministration(IdentityDb db, IPasswordHasher passwords)
{
    public async Task ProvisionRuntimeAsync(string password, CancellationToken ct)
    {
        if (password.Length is < 32 or > 128) throw new ArgumentException("Runtime credential length is invalid.");
        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @pwd nvarchar(128)={password}; DECLARE @sql nvarchar(max); IF USER_ID(N'identity_api') IS NULL SET @sql=N'CREATE USER [identity_api] WITH PASSWORD='+QUOTENAME(@pwd, CHAR(39)); ELSE SET @sql=N'ALTER USER [identity_api] WITH PASSWORD='+QUOTENAME(@pwd, CHAR(39)); EXEC sp_executesql @sql; GRANT SELECT,INSERT,UPDATE,DELETE ON dbo.IdentityUsers TO identity_api; GRANT SELECT,INSERT,UPDATE,DELETE ON dbo.IdentityHouses TO identity_api; GRANT SELECT,INSERT,UPDATE,DELETE ON dbo.IdentityMemberships TO identity_api; GRANT SELECT,INSERT,UPDATE,DELETE ON dbo.IdentitySessions TO identity_api; GRANT SELECT,INSERT,UPDATE,DELETE ON dbo.IdentityRefreshTokens TO identity_api; GRANT SELECT,INSERT,UPDATE,DELETE ON dbo.IdentityAssertions TO identity_api; GRANT SELECT ON dbo.IdentitySchemaVersions TO identity_api;", ct);
    }
    public async Task MigrateAsync(CancellationToken ct)
    {
        var assembly = typeof(IdentityAdministration).Assembly;
        var name = assembly.GetManifestResourceNames().Single(x => x.EndsWith("001_identity.sql", StringComparison.Ordinal));
        await using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        await db.Database.ExecuteSqlRawAsync(await reader.ReadToEndAsync(ct), ct);
    }
    public async Task<(Guid UserId, Guid HouseId)> BootstrapAsync(string email, string password, string houseName, CancellationToken ct)
    {
        if (!MailAddress.TryCreate(email, out var address) || address.Address != email || email.Length > 254) throw new ArgumentException("Provide a valid administrator email.");
        if (password.Length is < 14 or > 128) throw new ArgumentException("Use an application password between 14 and 128 characters.");
        if (string.IsNullOrWhiteSpace(houseName) || houseName.Length > 200) throw new ArgumentException("Provide the house name.");
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var existing = await db.Users.SingleOrDefaultAsync(x => x.NormalizedEmail == email.ToUpperInvariant(), ct);
        if (existing is not null)
        {
            if (!existing.Enabled || !passwords.Verify(existing.PasswordHash, password)) throw new InvalidOperationException("Existing administrator credentials do not match; bootstrap will not reset them.");
            var membership = await db.Memberships.SingleOrDefaultAsync(x => x.UserId == existing.Id && x.Role == "admin", ct) ?? throw new InvalidOperationException("The existing account is not a bootstrap administrator.");
            await transaction.CommitAsync(ct); return (existing.Id, membership.HouseId);
        }
        if (await db.Users.AnyAsync(ct)) throw new InvalidOperationException("Bootstrap is only allowed on an empty Identity database.");
        var userId = Guid.NewGuid(); var houseId = Guid.NewGuid();
        db.Users.Add(new() { Id = userId, Email = email, NormalizedEmail = email.ToUpperInvariant(), PasswordHash = passwords.Hash(password) });
        db.Houses.Add(new() { Id = houseId, Name = houseName });
        db.Memberships.Add(new() { UserId = userId, HouseId = houseId, Role = "admin" });
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return (userId, houseId);
    }
}
