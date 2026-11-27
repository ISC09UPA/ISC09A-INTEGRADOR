using AlmacenInteligente.Application.Usuarios;
using AlmacenInteligente.Domain.Common;
using AlmacenInteligente.Domain.Iot;
using AlmacenInteligente.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AlmacenInteligente.Infrastructure.Seguridad;

/// <summary>Cada usuario tiene exactamente un rol.</summary>
internal sealed class GestorUsuarios(UserManager<Usuario> usuarios, AlmacenDbContext db) : IGestorUsuarios
{
    public async Task<IReadOnlyList<UsuarioDto>> ListarAsync(string? rol, CancellationToken ct = default)
    {
        var lista = rol is null
            ? await usuarios.Users.OrderBy(u => u.Nombre).ToListAsync(ct)
            : (await usuarios.GetUsersInRoleAsync(rol)).OrderBy(u => u.Nombre).ToList();

        return await ADtos(lista, ct);
    }

    public async Task<UsuarioDto?> ObtenerAsync(Guid id, CancellationToken ct = default) =>
        await usuarios.FindByIdAsync(id.ToString()) is { } u ? await ADto(u, ct) : null;

    public async Task<UsuarioDto?> ObtenerPorNfcAsync(string codigoNfc, CancellationToken ct = default) =>
        await db.Users.FirstOrDefaultAsync(u => u.CodigoNfc == codigoNfc, ct) is { } u ? await ADto(u, ct) : null;

    public Task<bool> ExisteNfcAsync(string codigoNfc, Guid? excepto, CancellationToken ct = default) =>
        db.Users.AnyAsync(u => u.CodigoNfc == codigoNfc && u.Id != excepto, ct);

    public async Task<IReadOnlyList<UsuarioDto>> ListarConPermisoAsync(string permiso, CancellationToken ct = default)
    {
        var roles = db.RoleClaims.Where(c => c.ClaimType == UsuarioActualHttp.ClaimPermiso && c.ClaimValue == permiso).Select(c => c.RoleId);
        var conRol = db.UserRoles.Where(ur => roles.Contains(ur.RoleId)).Select(ur => ur.UserId);
        var lista = await db.Users.Where(u => u.Activo && conRol.Contains(u.Id)).OrderBy(u => u.Nombre).ToListAsync(ct);
        return await ADtos(lista, ct);
    }

    public async Task<UsuarioDto> CrearAsync(string nombre, string correo, string rol, string contrasena, string? codigoNfc, CancellationToken ct = default)
    {
        var usuario = new Usuario { Nombre = nombre, UserName = correo, Email = correo, EmailConfirmed = true, CodigoNfc = codigoNfc };
        Verificar(await usuarios.CreateAsync(usuario, contrasena));
        Verificar(await usuarios.AddToRoleAsync(usuario, rol));
        return await ADto(usuario, ct);
    }

    public async Task<UsuarioDto> ActualizarAsync(Guid id, string nombre, string rol, bool activo, string? codigoNfc, CancellationToken ct = default)
    {
        var usuario = await Cargar(id);
        usuario.Nombre = nombre;
        usuario.Activo = activo;
        usuario.CodigoNfc = codigoNfc;
        // Cambiar el sello invalida las cookies vigentes, así un usuario desactivado pierde la sesión.
        Verificar(await usuarios.UpdateAsync(usuario));
        Verificar(await usuarios.UpdateSecurityStampAsync(usuario));

        var actuales = await usuarios.GetRolesAsync(usuario);
        if (!actuales.SequenceEqual([rol]))
        {
            Verificar(await usuarios.RemoveFromRolesAsync(usuario, actuales));
            Verificar(await usuarios.AddToRoleAsync(usuario, rol));
        }
        return await ADto(usuario, ct);
    }

    public async Task RestablecerContrasenaAsync(Guid id, string contrasena, CancellationToken ct = default)
    {
        var usuario = await Cargar(id);
        var token = await usuarios.GeneratePasswordResetTokenAsync(usuario);
        Verificar(await usuarios.ResetPasswordAsync(usuario, token, contrasena));
    }

    private async Task<Usuario> Cargar(Guid id) =>
        await usuarios.FindByIdAsync(id.ToString()) ?? throw new DomainException("El usuario no existe.");

    private async Task<UsuarioDto> ADto(Usuario u, CancellationToken ct) => (await ADtos([u], ct))[0];

    /// <summary>Con el conteo de fotos de rostro (y cuántas entraron al modelo publicado) en una sola consulta.</summary>
    private async Task<IReadOnlyList<UsuarioDto>> ADtos(IReadOnlyList<Usuario> lista, CancellationToken ct)
    {
        var ids = lista.Select(u => u.Id).ToList();
        var fotos = await db.FotosRostro.AsNoTracking()
            .Where(f => ids.Contains(f.UsuarioId))
            .GroupBy(f => f.UsuarioId)
            .Select(g => new { UsuarioId = g.Key, Total = g.Count(), Entrenadas = g.Count(f => f.EntrenadaEn != null) })
            .ToDictionaryAsync(f => f.UsuarioId, ct);

        var resultado = new List<UsuarioDto>(lista.Count);
        foreach (var u in lista)
        {
            var (total, entrenadas) = fotos.TryGetValue(u.Id, out var f) ? (f.Total, f.Entrenadas) : (0, 0);
            resultado.Add(new UsuarioDto(u.Id, u.Nombre, u.Email ?? string.Empty,
                (await usuarios.GetRolesAsync(u)).FirstOrDefault() ?? string.Empty, u.Activo,
                u.CodigoNfc, total, FotoRostro.Estado(total, entrenadas)));
        }
        return resultado;
    }

    private static void Verificar(IdentityResult resultado)
    {
        if (!resultado.Succeeded)
            throw new DomainException(string.Join(" ", resultado.Errors.Select(e => e.Description)));
    }
}
