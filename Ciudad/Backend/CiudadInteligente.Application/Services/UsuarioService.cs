using System.Text.RegularExpressions;
using CiudadInteligente.Application.DTOs;
using CiudadInteligente.Application.Exceptions;
using CiudadInteligente.Application.Interfaces;
using CiudadInteligente.Application.Mapping;
using CiudadInteligente.Domain.Entities;
using CiudadInteligente.Domain.Enums;

namespace CiudadInteligente.Application.Services;

public class UsuarioService(IUsuarioRepository usuarios, IHasherContrasenas hasher) : IUsuarioService
{
    public async Task<IReadOnlyList<UsuarioDto>> ListarAsync() =>
        (await usuarios.ListarAsync()).Select(u => u.ADto()).ToList();

    public async Task<UsuarioDto> CrearAsync(CrearUsuarioDto dto)
    {
        var nombreUsuario = (dto.Usuario ?? string.Empty).Trim();
        ReglasUsuario.ValidarNombreUsuario(nombreUsuario);
        ReglasUsuario.ValidarContrasena(dto.Contrasena);

        if (!RolesUsuario.TryParse(dto.Rol, out var rol))
        {
            throw new ReglaNegocioException($"Rol inválido: usa \"{RolesUsuario.Administrador}\" u \"{RolesUsuario.Operador}\".");
        }

        if (await usuarios.ObtenerPorNombreAsync(nombreUsuario) is not null)
        {
            throw new ReglaNegocioException($"Ya existe el usuario \"{nombreUsuario}\".");
        }

        var usuario = new Usuario
        {
            NombreUsuario = nombreUsuario,
            NombreCompleto = string.IsNullOrWhiteSpace(dto.NombreCompleto) ? nombreUsuario : dto.NombreCompleto.Trim(),
            Rol = rol,
            HashContrasena = hasher.Hash(dto.Contrasena),
            CreadoEn = DateTimeOffset.UtcNow
        };
        usuario.Id = await usuarios.CrearAsync(usuario);
        return usuario.ADto();
    }

    public async Task EliminarAsync(long id, long solicitanteId)
    {
        if (id == solicitanteId)
        {
            throw new ReglaNegocioException("No puedes borrar tu propio usuario.");
        }

        var usuario = await usuarios.ObtenerPorIdAsync(id)
            ?? throw new RecursoNoEncontradoException($"No existe el usuario {id}.");

        if (usuario.Rol == RolUsuario.Administrador)
        {
            var administradores = (await usuarios.ListarAsync()).Count(u => u.Rol == RolUsuario.Administrador);
            if (administradores <= 1)
            {
                throw new ReglaNegocioException("Debe quedar al menos un administrador.");
            }
        }

        await usuarios.EliminarAsync(id);
    }
}

/// <summary>Reglas para nombres de usuario y contrasenas.</summary>
public static partial class ReglasUsuario
{
    public const int LongitudMinimaContrasena = 8;

    [GeneratedRegex("^[a-zA-Z0-9._-]{3,30}$")]
    private static partial Regex NombreValido();

    public static void ValidarNombreUsuario(string nombre)
    {
        if (!NombreValido().IsMatch(nombre))
        {
            throw new ReglaNegocioException("El usuario debe tener de 3 a 30 letras, números, punto, guion o guion bajo.");
        }
    }

    public static void ValidarContrasena(string? contrasena)
    {
        if (string.IsNullOrEmpty(contrasena) || contrasena.Length < LongitudMinimaContrasena)
        {
            throw new ReglaNegocioException($"La contraseña debe tener al menos {LongitudMinimaContrasena} caracteres.");
        }
    }
}
