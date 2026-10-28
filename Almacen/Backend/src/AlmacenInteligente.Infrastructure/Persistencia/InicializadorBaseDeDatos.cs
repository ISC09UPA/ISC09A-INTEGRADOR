using System.Security.Claims;
using AlmacenInteligente.Application.Seguridad;
using AlmacenInteligente.Domain.Productos;
using AlmacenInteligente.Infrastructure.Seguridad;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AlmacenInteligente.Infrastructure.Persistencia;

public static class InicializadorBaseDeDatos
{
    /// <summary>
    /// Aplica migraciones, crea los 4 roles con sus permisos y, si no hay ningún superadmin,
    /// crea el de la sección "Superadmin" de la configuración.
    /// </summary>
    public static async Task InicializarAsync(IServiceProvider servicios, CancellationToken ct = default)
    {
        await using var scope = servicios.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(InicializadorBaseDeDatos));

        await sp.GetRequiredService<AlmacenDbContext>().Database.MigrateAsync(ct);

        var roles = sp.GetRequiredService<RoleManager<Rol>>();
        foreach (var (nombre, permisos) in MatrizPermisos.PorDefecto)
        {
            var rol = await roles.FindByNameAsync(nombre);
            if (rol is null)
            {
                rol = new Rol(nombre);
                await roles.CreateAsync(rol);
            }

            // Solo agrega lo que falta: un ajuste manual de la matriz en la base no se pisa.
            var existentes = (await roles.GetClaimsAsync(rol))
                .Where(c => c.Type == UsuarioActualHttp.ClaimPermiso)
                .Select(c => c.Value)
                .ToHashSet();
            foreach (var permiso in permisos.Except(existentes))
                await roles.AddClaimAsync(rol, new Claim(UsuarioActualHttp.ClaimPermiso, permiso));
        }

        var db = sp.GetRequiredService<AlmacenDbContext>();
        if (!await db.Unidades.AnyAsync(ct))
        {
            db.Unidades.AddRange(
                new Unidad("PZA", "Pieza", permiteFracciones: false),
                new Unidad("CJA", "Caja", permiteFracciones: false),
                new Unidad("PAQ", "Paquete", permiteFracciones: false),
                new Unidad("KG", "Kilogramo", permiteFracciones: true),
                new Unidad("LT", "Litro", permiteFracciones: true),
                new Unidad("M", "Metro", permiteFracciones: true));
            await db.SaveChangesAsync(ct);
        }

        var usuarios = sp.GetRequiredService<UserManager<Usuario>>();
        if ((await usuarios.GetUsersInRoleAsync(Roles.Superadmin)).Count > 0)
            return;

        var config = sp.GetRequiredService<IConfiguration>().GetSection("Superadmin");
        var correo = config["Correo"];
        var contrasena = config["Contrasena"];
        if (string.IsNullOrWhiteSpace(correo) || string.IsNullOrWhiteSpace(contrasena))
        {
            log.LogWarning("No hay superadmin y faltan Superadmin:Correo y Superadmin:Contrasena en la configuración.");
            return;
        }

        var superadmin = new Usuario { Nombre = config["Nombre"] ?? "Administrador", UserName = correo, Email = correo, EmailConfirmed = true };
        var resultado = await usuarios.CreateAsync(superadmin, contrasena);
        if (!resultado.Succeeded)
            throw new InvalidOperationException("No se pudo crear el superadmin: " + string.Join(" ", resultado.Errors.Select(e => e.Description)));
        await usuarios.AddToRoleAsync(superadmin, Roles.Superadmin);
        log.LogInformation("Superadmin {Correo} creado.", correo);
    }
}
