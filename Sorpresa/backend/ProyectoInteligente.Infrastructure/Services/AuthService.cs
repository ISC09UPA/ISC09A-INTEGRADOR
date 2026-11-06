using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LockerSmart.Application.DTOs;
using LockerSmart.Application.Interfaces;
using LockerSmart.Domain.Entities;
using LockerSmart.Domain.Enums;
using LockerSmart.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace LockerSmart.Infrastructure.Services;

public class AuthService(AppDbContext db, IConfiguration cfg) : IAuthService
{
    public async Task<(AuthResponse?, string?)> RegistrarAsync(RegistroRequest r)
    {
        var email = r.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(r.Nombre) || !email.Contains('@') || r.Password.Length < 6)
            return (null, "Datos inválidos: revisa nombre, correo y contraseña (mínimo 6 caracteres).");

        if (await db.Usuarios.AnyAsync(u => u.Email == email))
            return (null, "Ese correo ya está registrado.");

        var u = new Usuario
        {
            Nombre = r.Nombre.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(r.Password),
            Rol = Rol.Usuario            // el rol NUNCA viene del cliente
        };
        db.Usuarios.Add(u);
        await db.SaveChangesAsync();
        return (Respuesta(u), null);
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest r)
    {
        var email = r.Email.Trim().ToLowerInvariant();
        var u = await db.Usuarios.FirstOrDefaultAsync(x => x.Email == email);
        return u is not null && BCrypt.Net.BCrypt.Verify(r.Password, u.PasswordHash)
            ? Respuesta(u) : null;
    }

    public async Task<UsuarioDto?> ObtenerAsync(int id)
    {
        var u = await db.Usuarios.FindAsync(id);
        return u is null ? null : ADto(u);
    }

    private static UsuarioDto ADto(Usuario u) =>
        new(u.Id, u.Nombre, u.Email, u.Rol.ToString(), u.FaceId != null);

    private AuthResponse Respuesta(Usuario u) => new(GenerarToken(u), ADto(u));

    private string GenerarToken(Usuario u)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg["Jwt:Key"]!));
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, u.Id.ToString()),
            new Claim(ClaimTypes.Name, u.Nombre),
            new Claim(ClaimTypes.Email, u.Email),
            new Claim(ClaimTypes.Role, u.Rol.ToString())
        };
        var jwt = new JwtSecurityToken(
            issuer: cfg["Jwt:Issuer"],
            audience: cfg["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }
}