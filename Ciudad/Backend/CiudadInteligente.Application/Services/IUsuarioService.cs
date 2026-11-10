using CiudadInteligente.Application.DTOs;

namespace CiudadInteligente.Application.Services;

/// <summary>Alta, baja y lista de usuarios (solo administrador; lo usa UsuariosController).</summary>
public interface IUsuarioService
{
    Task<IReadOnlyList<UsuarioDto>> ListarAsync();

    Task<UsuarioDto> CrearAsync(CrearUsuarioDto dto);

    /// <param name="solicitanteId">Quien lo pide: nadie se puede borrar a si mismo.</param>
    Task EliminarAsync(long id, long solicitanteId);
}
