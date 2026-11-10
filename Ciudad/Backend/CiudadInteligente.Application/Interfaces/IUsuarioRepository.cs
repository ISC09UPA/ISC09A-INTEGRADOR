using CiudadInteligente.Domain.Entities;

namespace CiudadInteligente.Application.Interfaces;

/// <summary>Tabla Usuarios (EF Core + SQLite en Infrastructure).</summary>
public interface IUsuarioRepository
{
    Task<Usuario?> ObtenerPorNombreAsync(string nombreUsuario);

    Task<Usuario?> ObtenerPorIdAsync(long id);

    Task<IReadOnlyList<Usuario>> ListarAsync();

    /// <summary>Regresa el Id nuevo.</summary>
    Task<long> CrearAsync(Usuario usuario);

    Task ActualizarContrasenaAsync(long id, string hashContrasena);

    Task EliminarAsync(long id);
}
