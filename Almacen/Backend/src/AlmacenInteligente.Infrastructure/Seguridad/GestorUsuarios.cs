using AlmacenInteligente.Application.Usuarios;
using AlmacenInteligente.Domain.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AlmacenInteligente.Infrastructure.Seguridad;

/// <summary>Cada usuario tiene exactamente un rol.</summary>
internal sealed class GestorUsuarios(UserManager<Usuario> usuarios) : IGestorUsuarios
{
    public async Task<IReadOnlyList<UsuarioDto>> ListarAsync(string? rol, CancellationToken ct = default)
    {
        var lista = rol is null
            ? await usuarios.Users.OrderBy(u => u.Nombre).ToListAsync(ct)
            : (await usuarios.GetUsersInRoleAsync(rol)).OrderBy(u => u.Nombre).ToList();

        var resultado = new List<UsuarioDto>(lista.Count);
        foreach (var u in lista)
            resultado.Add(await ADto(u));
        return resultado;
    }

    public async Task<UsuarioDto?> ObtenerAsync(Guid id, CancellationToken ct = default) =>
        await usuarios.FindByIdAsync(id.ToString()) is { } u ? await ADto(u) : null;

    public async Task<UsuarioDto> CrearAsync(string nombre, string correo, string rol, string contrasena, CancellationToken ct = default)
    {
        var usuario = new Usuario { Nombre = nombre, UserName = correo, Email = correo, EmailConfirmed = true };
        Verificar(await usuarios.CreateAsync(usuario, contrasena));
        Verificar(await usuarios.AddToRoleAsync(usuario, rol));
        return await ADto(usuario);
    }

    public async Task<UsuarioDto> ActualizarAsync(Guid id, string nombre, string rol, bool activo, CancellationToken ct = default)
    {
        var usuario = await Cargar(id);
        usuario.Nombre = nombre;
        usuario.Activo = activo;
        // Cambiar el sello invalida las cookies vigentes, así un usuario desactivado pierde la sesión.
        Verificar(await usuarios.UpdateAsync(usuario));
        Verificar(await usuarios.UpdateSecurityStampAsync(usuario));

        var actuales = await usuarios.GetRolesAsync(usuario);
        if (!actuales.SequenceEqual([rol]))
        {
            Verificar(await usuarios.RemoveFromRolesAsync(usuario, actuales));
            Verificar(await usuarios.AddToRoleAsync(usuario, rol));
        }
        return await ADto(usuario);
    }

    public async Task RestablecerContrasenaAsync(Guid id, string contrasena, CancellationToken ct = default)
    {
        var usuario = await Cargar(id);
        var token = await usuarios.GeneratePasswordResetTokenAsync(usuario);
        Verificar(await usuarios.ResetPasswordAsync(usuario, token, contrasena));
    }

    private async Task<Usuario> Cargar(Guid id) =>
        await usuarios.FindByIdAsync(id.ToString()) ?? throw new DomainException("El usuario no existe.");

    private async Task<UsuarioDto> ADto(Usuario u) =>
        new(u.Id, u.Nombre, u.Email ?? string.Empty, (await usuarios.GetRolesAsync(u)).FirstOrDefault() ?? string.Empty, u.Activo);

    private static void Verificar(IdentityResult resultado)
    {
        if (!resultado.Succeeded)
            throw new DomainException(string.Join(" ", resultado.Errors.Select(e => e.Description)));
    }
}
