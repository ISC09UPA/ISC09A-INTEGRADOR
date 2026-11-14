using Microsoft.EntityFrameworkCore;

namespace CasaDomotica.Security.Infrastructure;

public sealed class SecurityAdministration(SecurityDb db)
{
    public async Task MigrateAsync(CancellationToken ct)
    {
        var assembly = typeof(SecurityAdministration).Assembly;
        var scripts = assembly.GetManifestResourceNames().Where(x => x.EndsWith(".sql", StringComparison.Ordinal)).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        if (scripts.Length == 0) throw new InvalidOperationException("Security migrations are missing.");
        foreach (var name in scripts) { await using var stream = assembly.GetManifestResourceStream(name)!; using var reader = new StreamReader(stream); await db.Database.ExecuteSqlRawAsync(await reader.ReadToEndAsync(ct), ct); }
    }
    public async Task ProvisionRuntimeAsync(string password, CancellationToken ct)
    {
        if (password.Length is < 32 or > 128) throw new ArgumentException("Invalid runtime credential length.");
        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @pwd nvarchar(128)={password}; DECLARE @sql nvarchar(max); IF USER_ID(N'security_api') IS NULL SET @sql=N'CREATE USER [security_api] WITH PASSWORD='+QUOTENAME(@pwd, CHAR(39)); ELSE SET @sql=N'ALTER USER [security_api] WITH PASSWORD='+QUOTENAME(@pwd, CHAR(39)); EXEC sp_executesql @sql; GRANT SELECT,INSERT,UPDATE,DELETE ON dbo.SecurityInbox TO security_api; GRANT SELECT,INSERT,UPDATE,DELETE ON dbo.SecurityModes TO security_api; GRANT SELECT,INSERT,UPDATE,DELETE ON dbo.SecurityActivity TO security_api; GRANT SELECT,INSERT,UPDATE,DELETE ON dbo.SecurityIncidents TO security_api; GRANT SELECT,INSERT,UPDATE,DELETE ON dbo.SecurityCaptures TO security_api; GRANT SELECT,INSERT,UPDATE,DELETE ON dbo.SecurityCapturePolicies TO security_api; GRANT SELECT,INSERT,UPDATE,DELETE ON dbo.SecurityOutbox TO security_api;", ct);
    }
}
