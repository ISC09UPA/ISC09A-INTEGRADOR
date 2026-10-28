using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AlmacenInteligente.Application.Usuarios;
using AlmacenInteligente.Domain.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace AlmacenInteligente.Infrastructure.Seguridad;

/// <summary>Sección "Jwt" de la configuración. La web y la API deben compartir la misma clave.</summary>
public sealed record OpcionesJwt
{
    public required string Clave { get; init; }
    public required string Emisor { get; init; }
    public required string Audiencia { get; init; }
    public int HorasVigencia { get; init; } = 8;

    public static OpcionesJwt? Desde(IConfiguration configuracion) => configuracion.GetSection("Jwt").Get<OpcionesJwt>();

    public SymmetricSecurityKey LlaveFirma() => new(Encoding.UTF8.GetBytes(Clave));
}

internal sealed class EmisorTokens(UserManager<Usuario> usuarios, IUserClaimsPrincipalFactory<Usuario> fabrica, IConfiguration configuracion)
    : IEmisorTokens
{
    public async Task<TokenApiDto> EmitirAsync(Guid usuarioId, CancellationToken ct = default)
    {
        var jwt = OpcionesJwt.Desde(configuracion)
            ?? throw new DomainException("La API no está configurada: falta la sección Jwt (Clave, Emisor, Audiencia).");
        var usuario = await usuarios.FindByIdAsync(usuarioId.ToString());
        if (usuario is not { Activo: true })
            throw new DomainException("El usuario no existe o está desactivado.");

        var principal = await fabrica.CreateAsync(usuario);
        var expira = DateTimeOffset.UtcNow.AddHours(jwt.HorasVigencia);
        var token = new JwtSecurityToken(jwt.Emisor, jwt.Audiencia, principal.Claims, expires: expira.UtcDateTime,
            signingCredentials: new SigningCredentials(jwt.LlaveFirma(), SecurityAlgorithms.HmacSha256));

        return new TokenApiDto(new JwtSecurityTokenHandler().WriteToken(token), expira, usuario.Nombre,
            principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList(),
            principal.FindAll(UsuarioActualHttp.ClaimPermiso).Select(c => c.Value).ToList());
    }
}
