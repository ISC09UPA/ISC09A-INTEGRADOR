using CiudadInteligente.Application.Interfaces;
using CiudadInteligente.Domain.Entities;
using CiudadInteligente.Domain.Enums;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CiudadInteligente.API.Seguridad;

/// <summary>
/// Token JWT firmado con HMAC-SHA256. Lleva el id ("sub"), el usuario ("name"),
/// el nombre completo y el rol ("role"); con el rol la API decide que puede
/// hacer cada quien ([Authorize(Policy = ...)]).
/// </summary>
public sealed class GeneradorTokenJwt(OpcionesJwt opciones) : IGeneradorToken
{
    public const string ClaimId = "sub";
    public const string ClaimUsuario = "name";
    public const string ClaimRol = "role";
    public const string ClaimNombre = "nombre";

    private readonly JsonWebTokenHandler _manejador = new();

    public (string Token, DateTimeOffset ExpiraEn) Generar(Usuario usuario)
    {
        var ahora = DateTimeOffset.UtcNow;
        var expira = ahora.Add(opciones.Validez);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = opciones.Emisor,
            Audience = opciones.Emisor,
            IssuedAt = ahora.UtcDateTime,
            NotBefore = ahora.UtcDateTime,
            Expires = expira.UtcDateTime,
            Claims = new Dictionary<string, object>
            {
                [ClaimId] = usuario.Id.ToString(),
                [ClaimUsuario] = usuario.NombreUsuario,
                [ClaimNombre] = usuario.NombreCompleto,
                [ClaimRol] = RolesUsuario.Texto(usuario.Rol)
            },
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(opciones.Clave), SecurityAlgorithms.HmacSha256)
        };

        return (_manejador.CreateToken(descriptor), expira);
    }
}
