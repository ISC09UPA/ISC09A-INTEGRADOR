using System.Security.Claims;

namespace CiudadInteligente.API.Seguridad;

/// <summary>
/// Quien puede hacer que (se aplican con [Authorize(Policy = ...)]):
///   - Ciudadano (usuario normal, sin cuenta): solo api/publico/* ([AllowAnonymous]).
///   - Sin politica: cualquier usuario con sesion (administrador u operador).
///   - SoloAdministrador: control manual del cruce y alta/baja de usuarios.
///   - EnviarConteos: la camara/simulador (X-Api-Key) o un administrador.
/// </summary>
public static class Politicas
{
    public const string SoloAdministrador = "SoloAdministrador";
    public const string EnviarConteos = "EnviarConteos";

    /// <summary>Limite de peticiones por IP a api/publico/* (ver Program.cs).</summary>
    public const string LimitePublico = "publico";

    /// <summary>Limite de intentos de login (ver Program.cs).</summary>
    public const string LimiteLogin = "login";

    /// <summary>Id del usuario con sesion (claim "sub" del token).</summary>
    public static long IdUsuario(this ClaimsPrincipal usuario) =>
        long.TryParse(usuario.FindFirst(GeneradorTokenJwt.ClaimId)?.Value, out var id) ? id : 0;
}
