using AlmacenInteligente.Application.Abstracciones;
using AlmacenInteligente.Application.CasosDeUso;
using AlmacenInteligente.Application.Consultas;
using AlmacenInteligente.Application.Usuarios;
using AlmacenInteligente.Domain.Common;

namespace AlmacenInteligente.Application.Seguridad;

/// <summary>
/// Cada usuario trabaja en un solo almacén: solo vende, recibe, traspasa desde él y ve su stock.
/// El superadmin no tiene almacén y ve todos. Un usuario sin almacén asignado no puede operar.
/// </summary>
public sealed class AlcanceAlmacen(IUsuarioActual usuario, IGestorUsuarios usuarios)
{
    public const string SinAlmacen = "No tienes un almacén asignado. Pide al superadmin que te asigne uno en Usuarios.";
    public const string OtroAlmacen = "Solo puedes trabajar con el almacén que tienes asignado.";

    private Guid? _almacen;
    private bool _cargado;

    /// <summary>El almacén del usuario actual; null si es superadmin (sin restricción).</summary>
    public async Task<Guid?> AlmacenAsync(CancellationToken ct = default)
    {
        if (_cargado)
            return _almacen;
        if (usuario.TieneRol(Roles.Superadmin))
            _almacen = null;
        else
        {
            var u = usuario.Id is { } id ? await usuarios.ObtenerAsync(id, ct) : null;
            _almacen = u?.AlmacenId ?? throw new DomainException(SinAlmacen);
        }
        _cargado = true;
        return _almacen;
    }

    /// <summary>Falla si el almacén no es el del usuario (el superadmin puede con todos).</summary>
    public async Task AsegurarAsync(Guid almacenId, CancellationToken ct = default)
    {
        if (await AlmacenAsync(ct) is { } mio && mio != almacenId)
            throw new DomainException(OtroAlmacen);
    }

    /// <summary>Almacén con que trabaja ese usuario; null si es superadmin. Para los workers del embebido, que no tienen sesión.</summary>
    public static Guid? De(UsuarioDto u) => u.Rol == Roles.Superadmin ? null : u.AlmacenId;

    /// <summary>Deja en cada producto solo la existencia de ese almacén (null = todas).</summary>
    public static IReadOnlyList<ProductoOpcionDto> Filtrar(IReadOnlyList<ProductoOpcionDto> productos, Guid? almacenId) =>
        almacenId is { } a ? productos.Select(p => p with { Existencias = p.Existencias.Where(e => e.AlmacenId == a).ToList() }).ToList() : productos;
}

/// <summary>El almacén asignado al usuario actual (null para el superadmin). La web y la app lo preseleccionan.</summary>
[Publica]
public sealed record ObtenerMiAlmacen : ISolicitud<MiAlmacenDto>;

public sealed record MiAlmacenDto(Guid? AlmacenId, string? Almacen, bool VeTodos);

internal sealed class MiAlmacenManejador(IUsuarioActual usuario, IGestorUsuarios usuarios) : IManejador<ObtenerMiAlmacen, MiAlmacenDto>
{
    public async Task<MiAlmacenDto> ManejarAsync(ObtenerMiAlmacen s, CancellationToken ct)
    {
        if (!usuario.EstaAutenticado)
            throw new NoAutenticadoException();
        var u = usuario.Id is { } id ? await usuarios.ObtenerAsync(id, ct) : null;
        var todos = usuario.TieneRol(Roles.Superadmin);
        return new MiAlmacenDto(todos ? null : u?.AlmacenId, todos ? null : u?.Almacen, todos);
    }
}
