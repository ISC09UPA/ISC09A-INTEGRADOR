using AlmacenInteligente.Application.Abstracciones;
using AlmacenInteligente.Application.CasosDeUso;
using AlmacenInteligente.Application.Seguridad;
using AlmacenInteligente.Domain.Common;
using FluentValidation;

namespace AlmacenInteligente.Application.Usuarios;

public sealed record UsuarioDto(Guid Id, string Nombre, string Correo, string Rol, bool Activo);

/// <summary>Puerto hacia el almacén de usuarios (ASP.NET Core Identity).</summary>
public interface IGestorUsuarios
{
    Task<IReadOnlyList<UsuarioDto>> ListarAsync(string? rol, CancellationToken ct = default);
    Task<UsuarioDto?> ObtenerAsync(Guid id, CancellationToken ct = default);

    /// <summary>Lanza DomainException si el correo ya existe o la contraseña no cumple la política.</summary>
    Task<UsuarioDto> CrearAsync(string nombre, string correo, string rol, string contrasena, CancellationToken ct = default);

    Task<UsuarioDto> ActualizarAsync(Guid id, string nombre, string rol, bool activo, CancellationToken ct = default);
    Task RestablecerContrasenaAsync(Guid id, string contrasena, CancellationToken ct = default);
}

[RequierePermiso(Permisos.Usuarios.Gestionar)]
public sealed record ListarUsuarios(string? Rol = null) : ISolicitud<IReadOnlyList<UsuarioDto>>;

/// <summary>Gerentes activos, para elegir a quién enviar la aprobación de un producto.</summary>
[RequierePermiso(Permisos.Productos.Crear)]
public sealed record ListarGerentes : ISolicitud<IReadOnlyList<UsuarioDto>>;

[RequierePermiso(Permisos.Usuarios.Gestionar)]
public sealed record CrearUsuario(string Nombre, string Correo, string Rol, string Contrasena) : ISolicitud<UsuarioDto>;

[RequierePermiso(Permisos.Usuarios.Gestionar)]
public sealed record ActualizarUsuario(Guid Id, string Nombre, string Rol, bool Activo) : ISolicitud<UsuarioDto>;

[RequierePermiso(Permisos.Usuarios.Gestionar)]
public sealed record RestablecerContrasena(Guid Id, string Contrasena) : ISolicitud<bool>;

internal sealed class CrearUsuarioValidador : AbstractValidator<CrearUsuario>
{
    public CrearUsuarioValidador()
    {
        RuleFor(x => x.Nombre).NotEmpty().WithMessage("El nombre es obligatorio.").MaximumLength(200);
        RuleFor(x => x.Correo).NotEmpty().EmailAddress().WithMessage("El correo no es válido.");
        RuleFor(x => x.Rol).Must(Roles.Todos.Contains).WithMessage("El rol no existe.");
        RuleFor(x => x.Contrasena).MinimumLength(8).WithMessage("La contraseña debe tener al menos 8 caracteres.");
    }
}

internal sealed class ActualizarUsuarioValidador : AbstractValidator<ActualizarUsuario>
{
    public ActualizarUsuarioValidador()
    {
        RuleFor(x => x.Nombre).NotEmpty().WithMessage("El nombre es obligatorio.").MaximumLength(200);
        RuleFor(x => x.Rol).Must(Roles.Todos.Contains).WithMessage("El rol no existe.");
    }
}

internal sealed class RestablecerContrasenaValidador : AbstractValidator<RestablecerContrasena>
{
    public RestablecerContrasenaValidador() =>
        RuleFor(x => x.Contrasena).MinimumLength(8).WithMessage("La contraseña debe tener al menos 8 caracteres.");
}

internal sealed class UsuariosManejador(IGestorUsuarios gestor, IUsuarioActual usuario) :
    IManejador<ListarUsuarios, IReadOnlyList<UsuarioDto>>,
    IManejador<ListarGerentes, IReadOnlyList<UsuarioDto>>,
    IManejador<CrearUsuario, UsuarioDto>,
    IManejador<ActualizarUsuario, UsuarioDto>,
    IManejador<RestablecerContrasena, bool>
{
    public Task<IReadOnlyList<UsuarioDto>> ManejarAsync(ListarUsuarios s, CancellationToken ct) => gestor.ListarAsync(s.Rol, ct);

    public async Task<IReadOnlyList<UsuarioDto>> ManejarAsync(ListarGerentes s, CancellationToken ct) =>
        (await gestor.ListarAsync(Roles.Gerente, ct)).Where(u => u.Activo).ToList();

    public Task<UsuarioDto> ManejarAsync(CrearUsuario s, CancellationToken ct) =>
        gestor.CrearAsync(s.Nombre.Trim(), s.Correo.Trim(), s.Rol, s.Contrasena, ct);

    public async Task<UsuarioDto> ManejarAsync(ActualizarUsuario s, CancellationToken ct)
    {
        var actual = await gestor.ObtenerAsync(s.Id, ct) ?? throw new NoEncontradoException("el usuario", s.Id);
        if (s.Id == usuario.Id && (!s.Activo || s.Rol != actual.Rol))
            throw new DomainException("No puedes desactivarte ni cambiarte el rol a ti mismo.");
        return await gestor.ActualizarAsync(s.Id, s.Nombre.Trim(), s.Rol, s.Activo, ct);
    }

    public async Task<bool> ManejarAsync(RestablecerContrasena s, CancellationToken ct)
    {
        await gestor.RestablecerContrasenaAsync(s.Id, s.Contrasena, ct);
        return true;
    }
}
