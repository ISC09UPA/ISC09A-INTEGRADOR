using LockerSmart.Domain.Entities;
using LockerSmart.Domain.Enums;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LockerSmart.Infrastructure.Data;

public static class DatabaseInitializer
{
    /// <summary>Aplica las migraciones pendientes y siembra los datos iniciales (lockers y administrador).</summary>
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration config)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(DatabaseInitializer).FullName ?? nameof(DatabaseInitializer));

        try
        {
            // Crea la base y aplica las migraciones pendientes en Azure SQL
            await db.Database.MigrateAsync();

            if (!await db.Lockers.AnyAsync())
            {
                db.Lockers.AddRange(
                    new Locker { Codigo = "L-01", PesoMaximoKg = 10 },
                    new Locker { Codigo = "L-02", PesoMaximoKg = 10 },
                    new Locker { Codigo = "L-03", PesoMaximoKg = 20 });
                await db.SaveChangesAsync();
            }

            var username = (config["Admin:Username"] ?? "lockeradmin")
                .Trim()
                .ToLowerInvariant();

            var password = config["Admin:Password"]
                ?? throw new InvalidOperationException(
                    "No se configuró Admin:Password en User Secrets.");

            var admin = await db.Usuarios.FirstOrDefaultAsync(
                u => u.Email == username);

            if (admin is null)
            {
                admin = new Usuario
                {
                    Nombre = "Administrador de pruebas",
                    Email = username,
                    Rol = Rol.Administrador
                };

                db.Usuarios.Add(admin);
            }

            admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
            admin.Rol = Rol.Administrador;

            await db.SaveChangesAsync();
        }
        catch (SqlException ex)
        {
            logger.LogWarning(ex, "No se pudo conectar con la base de datos; la API iniciará sin datos iniciales. Revisa la cadena de conexión y el firewall de Azure SQL.");
        }
    }
}
