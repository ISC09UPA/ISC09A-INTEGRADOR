using AlmacenInteligente.Application.Abstracciones;
using AlmacenInteligente.Application.CasosDeUso;
using AlmacenInteligente.Application.Seguridad;

namespace AlmacenInteligente.Application.Usuarios;

public sealed record TokenApiDto(string Token, DateTimeOffset Expira, string Nombre, IReadOnlyList<string> Roles, IReadOnlyList<string> Permisos);

/// <summary>Emite el JWT de la API para un usuario, con sus roles y permisos actuales. Lo usan la API (login) y Blazor.</summary>
public interface IEmisorTokens
{
    Task<TokenApiDto> EmitirAsync(Guid usuarioId, CancellationToken ct = default);
}

/// <summary>Token de la API para el usuario que ya inició sesión en la web (sin volver a pedir contraseña).</summary>
[RequierePermiso(Permisos.Api.Acceder)]
public sealed record GenerarTokenApi : ISolicitud<TokenApiDto>;

internal sealed class TokenApiManejador(IEmisorTokens emisor, IUsuarioActual usuario) : IManejador<GenerarTokenApi, TokenApiDto>
{
    public Task<TokenApiDto> ManejarAsync(GenerarTokenApi s, CancellationToken ct) => emisor.EmitirAsync(usuario.Id!.Value, ct);
}
