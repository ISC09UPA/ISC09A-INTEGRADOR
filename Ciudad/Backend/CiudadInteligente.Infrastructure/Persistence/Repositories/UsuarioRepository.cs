using CiudadInteligente.Application.Interfaces;
using CiudadInteligente.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CiudadInteligente.Infrastructure.Persistence.Repositories;

/// <summary>Tabla Usuarios con Entity Framework Core (SQLite).</summary>
public sealed class UsuarioRepository(AppDbContext db) : IUsuarioRepository
{
    public Task<Usuario?> ObtenerPorNombreAsync(string nombreUsuario) =>
        // la columna es COLLATE NOCASE: "Admin" y "admin" son el mismo usuario
        db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.NombreUsuario == nombreUsuario);

    public Task<Usuario?> ObtenerPorIdAsync(long id) =>
        db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);

    public async Task<IReadOnlyList<Usuario>> ListarAsync() =>
        await db.Usuarios.AsNoTracking()
            .OrderBy(u => u.Rol)
            .ThenBy(u => u.NombreUsuario)
            .ToListAsync();

    public async Task<long> CrearAsync(Usuario usuario)
    {
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();
        return usuario.Id;
    }

    public Task ActualizarContrasenaAsync(long id, string hashContrasena) =>
        db.Usuarios.Where(u => u.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.HashContrasena, hashContrasena));

    public Task EliminarAsync(long id) =>
        db.Usuarios.Where(u => u.Id == id).ExecuteDeleteAsync();
}
