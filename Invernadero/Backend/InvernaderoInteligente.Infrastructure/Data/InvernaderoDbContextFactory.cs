using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InvernaderoInteligente.Infrastructure.Data;

public sealed class InvernaderoDbContextFactory : IDesignTimeDbContextFactory<InvernaderoDbContext>
{
    public InvernaderoDbContext CreateDbContext(string[] args)
    {
        var usarSqlite = string.Equals(
            Environment.GetEnvironmentVariable("Database__Provider"),
            "Sqlite",
            StringComparison.OrdinalIgnoreCase);
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Invernadero")
            ?? (usarSqlite
                ? "Data Source=invernadero.db"
                : "Server=(localdb)\\MSSQLLocalDB;Database=Invernadero;Trusted_Connection=True;TrustServerCertificate=True");

        var options = new DbContextOptionsBuilder<InvernaderoDbContext>();
        if (usarSqlite)
            options.UseSqlite(connectionString);
        else
            options.UseSqlServer(connectionString);

        return new InvernaderoDbContext(options.Options);
    }
}
