namespace CiudadInteligente.Application.DTOs;

/// <summary>Cuerpo de POST api/auth/login.</summary>
public record LoginDto(string Usuario, string Contrasena);

/// <summary>Lo que regresa el login: el token va en cada peticion como "Authorization: Bearer ...".</summary>
public record SesionDto(string Token, DateTimeOffset ExpiraEn, UsuarioDto Usuario);

/// <param name="Rol">administrador u operador.</param>
public record UsuarioDto(long Id, string Usuario, string NombreCompleto, string Rol, DateTimeOffset CreadoEn);

/// <summary>Cuerpo de POST api/usuarios (solo administrador).</summary>
public record CrearUsuarioDto(string Usuario, string NombreCompleto, string Contrasena, string Rol);

/// <summary>Cuerpo de POST api/auth/contrasena.</summary>
public record CambiarContrasenaDto(string Actual, string Nueva);
