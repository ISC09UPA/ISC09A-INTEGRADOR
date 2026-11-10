using System.Security.Cryptography;
using System.Text;

namespace CiudadInteligente.API.Seguridad;

/// <summary>
/// Configuracion del token de sesion ("Jwt" en appsettings.json).
/// La clave de firma va en dotnet user-secrets ("Jwt:Clave", 32+ caracteres),
/// nunca en el repo. Si no esta, se genera una al azar al arrancar: funciona,
/// pero cada vez que reinicias el backend todos tienen que iniciar sesion otra vez.
/// </summary>
public sealed class OpcionesJwt
{
    public required string Emisor { get; init; }
    public required byte[] Clave { get; init; }
    public required TimeSpan Validez { get; init; }
    public required bool ClaveGenerada { get; init; }

    public static OpcionesJwt Desde(IConfiguration config)
    {
        var texto = config["Jwt:Clave"];
        var generada = string.IsNullOrWhiteSpace(texto) || Encoding.UTF8.GetByteCount(texto) < 32;

        return new OpcionesJwt
        {
            Emisor = config["Jwt:Emisor"] is { Length: > 0 } emisor ? emisor : "CiudadInteligente",
            Clave = generada ? RandomNumberGenerator.GetBytes(64) : Encoding.UTF8.GetBytes(texto!),
            Validez = TimeSpan.FromHours(double.TryParse(config["Jwt:HorasValidez"], out var horas) && horas > 0 ? horas : 12),
            ClaveGenerada = generada
        };
    }
}
