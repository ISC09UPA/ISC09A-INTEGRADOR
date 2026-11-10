using System.Security.Cryptography;
using CiudadInteligente.Application.Interfaces;
using CiudadInteligente.Domain.Entities;
using CiudadInteligente.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CiudadInteligente.Infrastructure.Persistence;

/// <summary>
/// La primera vez que arranca la API (tabla Usuarios vacia) crea dos cuentas:
///   admin    (administrador)
///   operador (operador)
/// Sus contrasenas salen de dotnet user-secrets:
///   Usuarios:ContrasenaAdmin y Usuarios:ContrasenaOperador
/// Si no estan, se generan al azar y se muestran UNA vez en la consola.
/// Despues se cambian desde la app (Control > Usuarios) o se crean otras.
/// </summary>
public static class InicializadorUsuarios
{
    public static void Inicializar(
        AppDbContext db,
        IHasherContrasenas hasher,
        IConfiguration config,
        ILogger logger)
    {
        if (db.Usuarios.Any()) return;

        Crear(db, hasher, logger, "admin", "Administrador del cruce", RolUsuario.Administrador,
            config["Usuarios:ContrasenaAdmin"]);
        Crear(db, hasher, logger, "operador", "Operador de tráfico", RolUsuario.Operador,
            config["Usuarios:ContrasenaOperador"]);
        db.SaveChanges();
    }

    private static void Crear(
        AppDbContext db,
        IHasherContrasenas hasher,
        ILogger logger,
        string nombreUsuario,
        string nombreCompleto,
        RolUsuario rol,
        string? contrasenaConfigurada)
    {
        var generada = string.IsNullOrWhiteSpace(contrasenaConfigurada);
        var contrasena = generada ? ContrasenaAleatoria() : contrasenaConfigurada!;

        var usuario = new Usuario
        {
            NombreUsuario = nombreUsuario,
            NombreCompleto = nombreCompleto,
            Rol = rol,
            HashContrasena = hasher.Hash(contrasena),
            CreadoEn = DateTimeOffset.UtcNow
        };

        db.Usuarios.Add(usuario);

        if (generada)
        {
            logger.LogWarning(
                "Usuario creado: {Usuario} ({Rol}) con contrasena {Contrasena} . Anotala: no se vuelve a mostrar. " +
                "Para elegirla tu, borra la tabla Usuarios y usa dotnet user-secrets set \"Usuarios:Contrasena{Clave}\" \"...\".",
                nombreUsuario, RolesUsuario.Texto(rol), contrasena, rol == RolUsuario.Administrador ? "Admin" : "Operador");
        }
        else
        {
            logger.LogInformation("Usuario creado: {Usuario} ({Rol}) con la contrasena de user-secrets.",
                nombreUsuario, RolesUsuario.Texto(rol));
        }
    }

    /// <summary>12 caracteres faciles de leer (sin 0/O ni 1/l).</summary>
    private static string ContrasenaAleatoria()
    {
        const string alfabeto = "abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return string.Create(12, alfabeto, (destino, letras) =>
        {
            for (var i = 0; i < destino.Length; i++)
            {
                destino[i] = letras[RandomNumberGenerator.GetInt32(letras.Length)];
            }
        });
    }
}
