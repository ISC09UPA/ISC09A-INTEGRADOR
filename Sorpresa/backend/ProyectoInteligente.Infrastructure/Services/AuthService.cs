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

public class AuthService(
    AppDbContext db,
    IConfiguration cfg,
    IBlobStorageService blobStorage,
    IFaceRecognitionService faceService) : IAuthService
{
    public async Task<(AuthResponse?, string?)> RegistrarAsync(
        RegistroRequest r, byte[]? foto = null, string? contentType = null, CancellationToken cancellationToken = default)
    {
        var email = (r.Email ?? "").Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(r.Nombre) || !email.Contains('@') ||
            string.IsNullOrWhiteSpace(r.Password) || r.Password.Length < 8)
            return (null, "Revisa nombre, correo y contraseña (mínimo 8 caracteres).");

        var tieneFoto = foto is { Length: > 0 };
        if (tieneFoto &&
            (foto!.Length > 5 * 1024 * 1024 ||
             contentType is not ("image/jpeg" or "image/png")))
            return (null, "La fotografía debe ser JPG o PNG y pesar máximo 5 MB.");

        if (await db.Usuarios.AnyAsync(u => u.Email == email, cancellationToken))
            return (null, "Ese correo ya está registrado.");

        var usuario = new Usuario
        {
            Nombre = r.Nombre.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(r.Password),
            Rol = Rol.Usuario
        };

        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync(cancellationToken);

        if (!tieneFoto)
            return (CrearRespuesta(usuario), null);

        var registrarRostro = bool.TryParse(cfg["FaceRecognition:Enabled"], out var habilitado) && habilitado;

        string? blobName = null;
        try
        {
            await using var stream = new MemoryStream(foto!);
            blobName = await blobStorage.UploadAsync(stream, contentType!, cancellationToken);
            usuario.FotoUrl = blobName;

            if (registrarRostro)
            {
                var sasUrl = await blobStorage.CreateReadSasUrlAsync(blobName, TimeSpan.FromMinutes(10), cancellationToken);
                usuario.FaceId = await faceService.RegisterPersonAsync(
                    $"usuario-{usuario.Id}-{usuario.Nombre}", sasUrl, cancellationToken);
            }

            await db.SaveChangesAsync(cancellationToken);
            return (CrearRespuesta(usuario), null);
        }
        catch (Exception ex)
        {
            db.Usuarios.Remove(usuario);
            await db.SaveChangesAsync(cancellationToken);
            if (blobName is not null)
                await blobStorage.DeleteAsync(blobName, cancellationToken);

            return (null, $"No se pudo completar el registro de la fotografía. Detalle: {ex.Message}");
        }
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest r, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(r.Email) || string.IsNullOrEmpty(r.Password))
            return null;

        var email = r.Email.Trim().ToLowerInvariant();
        var usuario = await db.Usuarios.FirstOrDefaultAsync(x => x.Email == email, cancellationToken);

        return usuario is not null && BCrypt.Net.BCrypt.Verify(r.Password, usuario.PasswordHash)
            ? CrearRespuesta(usuario)
            : null;
    }

    public async Task<FaceLoginResult> LoginConRostroAsync(
        byte[] foto, CancellationToken cancellationToken = default)
    {
        if (foto.Length == 0 || foto.Length > 5 * 1024 * 1024)
            return new(null, "La imagen está vacía o supera los 5 MB.");

        try
        {
            var personId = await faceService.IdentifyPersonAsync(foto, cancellationToken);
            if (string.IsNullOrWhiteSpace(personId))
                return new(null, "No se reconoció un rostro registrado.");

            var usuario = await db.Usuarios.FirstOrDefaultAsync(
                x => x.FaceId == personId, cancellationToken);

            return usuario is null
                ? new(null, "El rostro no está asociado a una cuenta activa.")
                : new(CrearRespuesta(usuario), null);
        }
        catch (Exception ex)
        {
            return new(null, $"No fue posible validar el rostro con Azure Face: {ex.Message}");
        }
    }

    public async Task<UsuarioDto?> ObtenerAsync(int id)
    {
        var usuario = await db.Usuarios.FindAsync(id);
        return usuario is null ? null : ADto(usuario);
    }

    private static UsuarioDto ADto(Usuario u) =>
        new(u.Id, u.Nombre, u.Email, u.Rol.ToString(), u.FaceId != null, u.FotoUrl);

    private AuthResponse CrearRespuesta(Usuario u) => new(GenerarToken(u), ADto(u));

    private string GenerarToken(Usuario u)
    {
        var jwtKey = cfg["Jwt:Key"] ??
            throw new InvalidOperationException("Falta configurar Jwt:Key.");
        if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
            throw new InvalidOperationException("Jwt:Key debe tener al menos 32 bytes.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
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
