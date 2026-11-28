using AlmacenInteligente.Application.Abstracciones;
using AlmacenInteligente.Application.CasosDeUso;
using AlmacenInteligente.Application.Seguridad;
using AlmacenInteligente.Domain.Common;
using AlmacenInteligente.Domain.Iot;
using FluentValidation;

namespace AlmacenInteligente.Application.Usuarios;

/// <param name="CodigoNfc">UID de la tarjeta NFC con la que entra por el acceso IoT.</param>
/// <param name="FotosRostro">Fotos de rostro cargadas para el reconocimiento facial.</param>
/// <param name="AlmacenId">Almacén donde trabaja: solo opera y ve el stock de ese almacén. Null en el superadmin.</param>
public sealed record UsuarioDto(
    Guid Id, string Nombre, string Correo, string Rol, bool Activo,
    string? CodigoNfc = null, int FotosRostro = 0, EstadoReconocimiento Reconocimiento = EstadoReconocimiento.SinFotos,
    Guid? AlmacenId = null, string? Almacen = null);

/// <summary>Puerto hacia el almacén de usuarios (ASP.NET Core Identity).</summary>
public interface IGestorUsuarios
{
    Task<IReadOnlyList<UsuarioDto>> ListarAsync(string? rol, CancellationToken ct = default);
    Task<UsuarioDto?> ObtenerAsync(Guid id, CancellationToken ct = default);

    /// <summary>Usuarios activos cuyo rol tiene ese permiso (p. ej. quienes reciben la alerta de bajo stock).</summary>
    Task<IReadOnlyList<UsuarioDto>> ListarConPermisoAsync(string permiso, CancellationToken ct = default);

    /// <summary>Lanza DomainException si el correo ya existe o la contraseña no cumple la política.</summary>
    Task<UsuarioDto> CrearAsync(string nombre, string correo, string rol, string contrasena, string? codigoNfc, Guid? almacenId, CancellationToken ct = default);

    Task<UsuarioDto> ActualizarAsync(Guid id, string nombre, string rol, bool activo, string? codigoNfc, Guid? almacenId, CancellationToken ct = default);

    /// <summary>El usuario dueño de esa tarjeta (UID ya normalizado), activo o no.</summary>
    Task<UsuarioDto?> ObtenerPorNfcAsync(string codigoNfc, CancellationToken ct = default);

    Task<bool> ExisteNfcAsync(string codigoNfc, Guid? excepto, CancellationToken ct = default);
    Task RestablecerContrasenaAsync(Guid id, string contrasena, CancellationToken ct = default);
}

[RequierePermiso(Permisos.Usuarios.Gestionar)]
public sealed record ListarUsuarios(string? Rol = null) : ISolicitud<IReadOnlyList<UsuarioDto>>;

/// <summary>Gerentes activos, para elegir a quién enviar la aprobación de un producto.</summary>
[RequierePermiso(Permisos.Productos.Crear)]
public sealed record ListarGerentes : ISolicitud<IReadOnlyList<UsuarioDto>>;

[RequierePermiso(Permisos.Usuarios.Gestionar)]
/// <param name="AlmacenId">Obligatorio salvo para el superadmin.</param>
public sealed record CrearUsuario(string Nombre, string Correo, string Rol, string Contrasena, string? CodigoNfc = null, Guid? AlmacenId = null) : ISolicitud<UsuarioDto>;

[RequierePermiso(Permisos.Usuarios.Gestionar)]
/// <param name="CodigoNfc">null conserva la tarjeta actual; "" la quita.</param>
/// <param name="AlmacenId">null conserva el almacén actual. Obligatorio salvo para el superadmin.</param>
public sealed record ActualizarUsuario(Guid Id, string Nombre, string Rol, bool Activo, string? CodigoNfc = null, Guid? AlmacenId = null) : ISolicitud<UsuarioDto>;

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

internal sealed class UsuariosManejador(IGestorUsuarios gestor, IUsuarioActual usuario, IAlmacenRepositorio almacenes) :
    IManejador<ListarUsuarios, IReadOnlyList<UsuarioDto>>,
    IManejador<ListarGerentes, IReadOnlyList<UsuarioDto>>,
    IManejador<CrearUsuario, UsuarioDto>,
    IManejador<ActualizarUsuario, UsuarioDto>,
    IManejador<RestablecerContrasena, bool>
{
    public Task<IReadOnlyList<UsuarioDto>> ManejarAsync(ListarUsuarios s, CancellationToken ct) => gestor.ListarAsync(s.Rol, ct);

    public async Task<IReadOnlyList<UsuarioDto>> ManejarAsync(ListarGerentes s, CancellationToken ct) =>
        (await gestor.ListarAsync(Roles.Gerente, ct)).Where(u => u.Activo).ToList();

    public async Task<UsuarioDto> ManejarAsync(CrearUsuario s, CancellationToken ct)
    {
        var nfc = await ValidarNfcAsync(s.CodigoNfc, null, ct);
        var almacen = await ValidarAlmacenAsync(s.Rol, s.AlmacenId, ct);
        return await gestor.CrearAsync(s.Nombre.Trim(), s.Correo.Trim(), s.Rol, s.Contrasena, nfc, almacen, ct);
    }

    public async Task<UsuarioDto> ManejarAsync(ActualizarUsuario s, CancellationToken ct)
    {
        var actual = await gestor.ObtenerAsync(s.Id, ct) ?? throw new NoEncontradoException("el usuario", s.Id);
        if (s.Id == usuario.Id && (!s.Activo || s.Rol != actual.Rol))
            throw new DomainException("No puedes desactivarte ni cambiarte el rol a ti mismo.");
        var nfc = s.CodigoNfc is null ? actual.CodigoNfc : await ValidarNfcAsync(s.CodigoNfc, s.Id, ct);
        var almacen = await ValidarAlmacenAsync(s.Rol, s.AlmacenId ?? actual.AlmacenId, ct);
        return await gestor.ActualizarAsync(s.Id, s.Nombre.Trim(), s.Rol, s.Activo, nfc, almacen, ct);
    }

    /// <summary>Todos llevan almacén salvo el superadmin, que ve todos (se le quita si lo tenía).</summary>
    private async Task<Guid?> ValidarAlmacenAsync(string rol, Guid? almacenId, CancellationToken ct)
    {
        if (rol == Roles.Superadmin)
            return null;
        if (almacenId is not { } id || id == Guid.Empty)
            throw new DomainException("Asigna el almacén donde trabaja el usuario.");
        var almacen = await almacenes.ObtenerAsync(id, ct) ?? throw new NoEncontradoException("el almacén", id);
        if (!almacen.Activo)
            throw new DomainException($"El almacén {almacen.Nombre} está inactivo.");
        return id;
    }

    private async Task<string?> ValidarNfcAsync(string? codigo, Guid? excepto, CancellationToken ct)
    {
        var nfc = CodigoNfc.Normalizar(codigo);
        if (nfc is not null && await gestor.ExisteNfcAsync(nfc, excepto, ct))
            throw new DomainException($"La tarjeta NFC {nfc} ya está asignada a otro usuario.");
        return nfc;
    }

    public async Task<bool> ManejarAsync(RestablecerContrasena s, CancellationToken ct)
    {
        await gestor.RestablecerContrasenaAsync(s.Id, s.Contrasena, ct);
        return true;
    }
}
