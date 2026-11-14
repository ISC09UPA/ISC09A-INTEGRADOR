using AlmacenInteligente.Application.Abstracciones;
using AlmacenInteligente.Application.CasosDeUso;
using AlmacenInteligente.Application.Seguridad;
using AlmacenInteligente.Application.Terceros;
using AlmacenInteligente.Domain.Common;
using AlmacenInteligente.Domain.Terceros;
using AlmacenInteligente.Domain.ValueObjects;
using FluentValidation;

namespace AlmacenInteligente.Application.Proveedores;

[RequierePermiso(Permisos.Proveedores.Ver)]
public sealed record ListarProveedores(string? Busqueda = null, bool IncluirInactivos = false, int NumeroPagina = 1, int Tamano = 20)
    : ISolicitud<Pagina<TerceroDto>>;

[RequierePermiso(Permisos.Proveedores.Ver)]
public sealed record ObtenerProveedor(Guid Id) : ISolicitud<TerceroDto>;

[RequierePermiso(Permisos.Proveedores.Crear)]
public sealed record CrearProveedor(DatosTercero Datos) : ISolicitud<TerceroDto>;

[RequierePermiso(Permisos.Proveedores.Editar)]
public sealed record EditarProveedor(Guid Id, DatosTercero Datos) : ISolicitud<TerceroDto>;

[RequierePermiso(Permisos.Proveedores.Editar)]
public sealed record CambiarEstadoProveedor(Guid Id, bool Activo) : ISolicitud<TerceroDto>;

internal sealed class CrearProveedorValidador : AbstractValidator<CrearProveedor>
{
    public CrearProveedorValidador() => RuleFor(x => x.Datos).NotNull().SetValidator(new DatosTerceroValidador());
}

internal sealed class EditarProveedorValidador : AbstractValidator<EditarProveedor>
{
    public EditarProveedorValidador() => RuleFor(x => x.Datos).NotNull().SetValidator(new DatosTerceroValidador());
}

internal sealed class ProveedoresManejador(
    IProveedorRepositorio proveedores, IUnidadDeTrabajo unidad, IUsuarioActual usuario, IReloj reloj) :
    IManejador<ListarProveedores, Pagina<TerceroDto>>,
    IManejador<ObtenerProveedor, TerceroDto>,
    IManejador<CrearProveedor, TerceroDto>,
    IManejador<EditarProveedor, TerceroDto>,
    IManejador<CambiarEstadoProveedor, TerceroDto>
{
    public async Task<Pagina<TerceroDto>> ManejarAsync(ListarProveedores s, CancellationToken ct)
    {
        var (elementos, total) = await proveedores.BuscarAsync(s.Busqueda, s.IncluirInactivos, s.NumeroPagina, s.Tamano, ct);
        return new Pagina<TerceroDto>(elementos.Select(TerceroDto.DeDominio).ToList(), total, s.NumeroPagina, s.Tamano);
    }

    public async Task<TerceroDto> ManejarAsync(ObtenerProveedor s, CancellationToken ct) =>
        TerceroDto.DeDominio(await Cargar(s.Id, ct));

    public async Task<TerceroDto> ManejarAsync(CrearProveedor s, CancellationToken ct)
    {
        var d = s.Datos;
        if (await proveedores.ExisteCodigoAsync(d.Codigo.Trim().ToUpperInvariant(), ct))
            throw new DomainException($"Ya existe un proveedor con el código {d.Codigo.Trim().ToUpperInvariant()}.");

        var proveedor = new Proveedor(d.Codigo, d.Nombre, Correo.Crear(d.Correo), Rfc.Crear(d.Rfc), d.Direccion.ADominio(),
            new Auditoria(usuario.Id!.Value, reloj.Ahora));
        proveedores.Agregar(proveedor);
        await unidad.GuardarAsync(ct);
        return TerceroDto.DeDominio(proveedor);
    }

    /// <summary>El código no se edita: identifica al proveedor en documentos ya emitidos.</summary>
    public async Task<TerceroDto> ManejarAsync(EditarProveedor s, CancellationToken ct)
    {
        var proveedor = await Cargar(s.Id, ct);
        var d = s.Datos;
        proveedor.Actualizar(d.Nombre, Correo.Crear(d.Correo), Rfc.Crear(d.Rfc), d.Direccion.ADominio());
        await unidad.GuardarAsync(ct);
        return TerceroDto.DeDominio(proveedor);
    }

    public async Task<TerceroDto> ManejarAsync(CambiarEstadoProveedor s, CancellationToken ct)
    {
        var proveedor = await Cargar(s.Id, ct);
        if (s.Activo) proveedor.Activar(); else proveedor.Desactivar();
        await unidad.GuardarAsync(ct);
        return TerceroDto.DeDominio(proveedor);
    }

    private async Task<Proveedor> Cargar(Guid id, CancellationToken ct) =>
        await proveedores.ObtenerAsync(id, ct) ?? throw new NoEncontradoException("el proveedor", id);
}
