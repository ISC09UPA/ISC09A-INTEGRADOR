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
        if (tieneFoto && !EsImagenValida(foto!, contentType))
            return (null, "La fotografía debe ser JPG o PNG válida y pesar máximo 5 MB.");

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
        catch
        {
            if (blobName is not null)
            {
                try
                {
                    await blobStorage.DeleteAsync(blobName, cancellationToken);
                }
                catch
                {
                    // La limpieza falló; debe registrarse para revisión posterior.
                }
            }

            db.Usuarios.Remove(usuario);
            await db.SaveChangesAsync(cancellationToken);

            return (null, "No se pudo completar el registro de la fotografía. Intenta nuevamente.");
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
                : new(CrearRespuesta(usuario, faceVerified: true), null);
        }
        catch (Exception)
        {
            return new(null, "No fue posible validar el rostro en este momento. Intenta nuevamente.");
        }
    }

    public async Task<UsuarioDto?> ObtenerAsync(int id)
    {
        var usuario = await db.Usuarios.FindAsync(id);
        return usuario is null ? null : ADto(usuario);
    }

    private static bool EsImagenValida(byte[] foto, string? contentType)
    {
        if (foto.Length < 8 || foto.Length > 5 * 1024 * 1024)
            return false;

        return contentType switch
        {
            "image/jpeg" =>
                foto.Length >= 3 &&
                foto[0] == 0xFF &&
                foto[1] == 0xD8 &&
                foto[2] == 0xFF,

            "image/png" =>
                foto.Length >= 8 &&
                foto[0] == 0x89 &&
                foto[1] == 0x50 &&
                foto[2] == 0x4E &&
                foto[3] == 0x47 &&
                foto[4] == 0x0D &&
                foto[5] == 0x0A &&
                foto[6] == 0x1A &&
                foto[7] == 0x0A,

            _ => false
        };
    }

    private static UsuarioDto ADto(Usuario u) =>
        new(u.Id, u.Nombre, u.Email, u.Rol.ToString(), u.FaceId != null, u.FotoUrl);

    //private AuthResponse CrearRespuesta(Usuario u) => new(GenerarToken(u), ADto(u));
    private AuthResponse CrearRespuesta(
    Usuario u,
    bool faceVerified = false)
    {
        return new AuthResponse(
            GenerarToken(u, faceVerified),
            ADto(u));
    }

    private string GenerarToken(Usuario u, bool faceVerified)
    {
        var jwtKey = cfg["Jwt:Key"] ??
            throw new InvalidOperationException("Falta configurar Jwt:Key.");

        if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
            throw new InvalidOperationException("Jwt:Key debe tener al menos 32 bytes.");

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtKey));

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, u.Id.ToString()),
            new Claim(ClaimTypes.Name, u.Nombre),
            new Claim(ClaimTypes.Email, u.Email),
            new Claim(ClaimTypes.Role, u.Rol.ToString())
        };

        if (faceVerified)
            claims.Add(new Claim("face_verified", "true"));

        var expiracion = faceVerified
            ? DateTime.UtcNow.AddMinutes(10)
            : DateTime.UtcNow.AddHours(8);

        var jwt = new JwtSecurityToken(
            issuer: cfg["Jwt:Issuer"],
            audience: cfg["Jwt:Audience"],
            claims: claims,
            expires: expiracion,
            signingCredentials: new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    
}
