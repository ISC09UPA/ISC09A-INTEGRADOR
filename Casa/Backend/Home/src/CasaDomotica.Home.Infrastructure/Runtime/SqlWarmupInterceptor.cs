using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Diagnostics;
namespace CasaDomotica.Home.Infrastructure.Runtime;
/// <summary>Retries only connection establishment while serverless SQL resumes; never replays a transaction or command.</summary>
public sealed class SqlWarmupInterceptor : DbConnectionInterceptor
{
    public override async ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    {
        if (result.IsSuppressed) return result;
        for (var attempt = 0; ; attempt++)
        {
            try { await connection.OpenAsync(cancellationToken); return InterceptionResult.Suppress(); }
            catch (SqlException error) when (attempt < 5 && error.Number is 40613 or 40197 or 40501 or 49918 or 49919 or 49920 or 10928 or 10929)
            {
                await connection.CloseAsync();
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }
    }
}
