using Microsoft.EntityFrameworkCore;

namespace CasaDomotica.Notifications.Infrastructure;

public sealed class NotificationsAdministration(NotificationsDb db)
{
    public async Task MigrateAsync(CancellationToken ct)
    {
        var assembly = typeof(NotificationsAdministration).Assembly;
        var scripts = assembly.GetManifestResourceNames()
            .Where(x => x.EndsWith(".sql", StringComparison.Ordinal))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
        if (scripts.Length == 0) throw new InvalidOperationException("Notifications migrations are missing.");
        foreach (var name in scripts)
        {
            await using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            await db.Database.ExecuteSqlRawAsync(await reader.ReadToEndAsync(ct), ct);
        }
    }

    public async Task ProvisionRuntimeAsync(string password, CancellationToken ct)
    {
        if (password.Length is < 32 or > 128) throw new ArgumentException("Invalid runtime credential length.");
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @pwd nvarchar(128)={password};
            DECLARE @sql nvarchar(max);
            IF USER_ID(N'notifications_api') IS NULL
                SET @sql=N'CREATE USER [notifications_api] WITH PASSWORD='+QUOTENAME(@pwd, CHAR(39));
            ELSE
                SET @sql=N'ALTER USER [notifications_api] WITH PASSWORD='+QUOTENAME(@pwd, CHAR(39));
            EXEC sp_executesql @sql;
            GRANT SELECT,INSERT,UPDATE,DELETE ON dbo.NotificationsInbox TO notifications_api;
            GRANT SELECT,INSERT,UPDATE,DELETE ON dbo.NotificationPreferences TO notifications_api;
            GRANT SELECT,INSERT,UPDATE,DELETE ON dbo.NotificationDeliveries TO notifications_api;
            """, ct);
    }
}
