using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using CiudadInteligente.Domain.Enums;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace CiudadInteligente.API.Seguridad;

/// <summary>
/// La camara de la Raspberry y el simulador no inician sesion: mandan el
/// encabezado "X-Api-Key" con la llave de "Dispositivos:ApiKey"
/// (dotnet user-secrets). Si coincide, la peticion entra con el rol
/// "dispositivo", que solo puede mandar conteos (POST api/trafico y /lote).
/// </summary>
public sealed class AutenticacionDispositivo(
    IOptionsMonitor<AuthenticationSchemeOptions> opciones,
    ILoggerFactory logger,
    UrlEncoder codificador,
    IConfiguration config) : AuthenticationHandler<AuthenticationSchemeOptions>(opciones, logger, codificador)
{
    public const string Esquema = "Dispositivo";
    public const string Encabezado = "X-Api-Key";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(Encabezado, out var valor))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var esperada = config["Dispositivos:ApiKey"];
        if (string.IsNullOrWhiteSpace(esperada))
        {
            return Task.FromResult(AuthenticateResult.Fail(
                "El backend no tiene configurada Dispositivos:ApiKey (dotnet user-secrets)."));
        }

        // comparacion en tiempo constante: no deja adivinar la llave por cuanto tarda
        var coincide = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(valor.ToString()),
            Encoding.UTF8.GetBytes(esperada));

        if (!coincide)
        {
            return Task.FromResult(AuthenticateResult.Fail("Llave de dispositivo incorrecta."));
        }

        var identidad = new ClaimsIdentity(
            [
                new Claim(GeneradorTokenJwt.ClaimUsuario, "dispositivo"),
                new Claim(GeneradorTokenJwt.ClaimRol, RolesUsuario.Dispositivo)
            ],
            Esquema,
            GeneradorTokenJwt.ClaimUsuario,
            GeneradorTokenJwt.ClaimRol);

        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identidad), Esquema)));
    }
}
