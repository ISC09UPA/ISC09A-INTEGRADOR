using AlmacenInteligente.Application.Abstracciones;
using AlmacenInteligente.Application.CasosDeUso;
using AlmacenInteligente.Application.Consultas;
using AlmacenInteligente.Application.Seguridad;
using AlmacenInteligente.Application.Terceros;
using AlmacenInteligente.Application.Usuarios;
using AlmacenInteligente.Domain.Common;
using AlmacenInteligente.Domain.Productos;
using FluentValidation;

namespace AlmacenInteligente.Application.Productos;

[RequierePermiso(Permisos.Productos.Ver)]
public sealed record ListarUnidades : ISolicitud<IReadOnlyList<UnidadDto>>;

[RequierePermiso(Permisos.Productos.Crear)]
public sealed record CrearUnidad(string Clave, string Nombre, bool PermiteFracciones) : ISolicitud<UnidadDto>;

[RequierePermiso(Permisos.Productos.Ver)]
public sealed record ListarProductos(FiltroProductos Filtro) : ISolicitud<Pagina<ProductoDto>>;

[RequierePermiso(Permisos.Productos.Ver)]
public sealed record ObtenerProducto(Guid Id) : ISolicitud<ProductoDto>;

/// <summary>Productos aprobados y activos para capturar documentos, con existencia por almacén.</summary>
[RequierePermiso(Permisos.Productos.Ver)]
public sealed record BuscarProductosDisponibles(string? Texto, int Maximo = 20) : ISolicitud<IReadOnlyList<ProductoOpcionDto>>;

/// <summary>
/// Quien puede aprobar (superadmin, gerente) crea el producto aprobado. Quien no, debe elegir al gerente
/// que recibirá la solicitud y el producto queda pendiente.
/// </summary>
[RequierePermiso(Permisos.Productos.Crear)]
public sealed record CrearProducto(string Referencia, string Descripcion, Guid UnidadId, Guid? GerenteAprobadorId)
    : ISolicitud<ProductoDto>;

[RequierePermiso(Permisos.Productos.Editar)]
public sealed record EditarProducto(Guid Id, string Descripcion, Guid UnidadId) : ISolicitud<ProductoDto>;

[RequierePermiso(Permisos.Productos.Editar)]
public sealed record CambiarEstadoProducto(Guid Id, bool Activo) : ISolicitud<ProductoDto>;

/// <summary>Quien puede dar de alta productos también les puede poner imagen.</summary>
[RequierePermiso(Permisos.Productos.Crear)]
public sealed record SubirImagenProducto(Guid Id, string NombreArchivo, string TipoContenido, byte[] Contenido)
    : ISolicitud<ProductoDto>;

[RequierePermiso(Permisos.Productos.Aprobar)]
public sealed record AprobarProducto(Guid Id) : ISolicitud<ProductoDto>;

[RequierePermiso(Permisos.Productos.Aprobar)]
public sealed record RechazarProducto(Guid Id, string Motivo) : ISolicitud<ProductoDto>;

internal sealed class CrearUnidadValidador : AbstractValidator<CrearUnidad>
{
    public CrearUnidadValidador()
    {
        RuleFor(x => x.Clave).NotEmpty().WithMessage("La clave es obligatoria.").MaximumLength(10);
        RuleFor(x => x.Nombre).NotEmpty().WithMessage("El nombre es obligatorio.").MaximumLength(60);
    }
}

internal sealed class CrearProductoValidador : AbstractValidator<CrearProducto>
{
    public CrearProductoValidador()
    {
        RuleFor(x => x.Referencia).NotEmpty().WithMessage("La referencia es obligatoria.").MaximumLength(50);
        RuleFor(x => x.Descripcion).NotEmpty().WithMessage("La descripción es obligatoria.").MaximumLength(500);
        RuleFor(x => x.UnidadId).NotEmpty().WithMessage("Elige la unidad.");
    }
}

internal sealed class SubirImagenValidador : AbstractValidator<SubirImagenProducto>
{
    public const int TamanoMaximo = 2 * 1024 * 1024;
    private static readonly string[] Tipos = ["image/jpeg", "image/png", "image/webp"];

    public SubirImagenValidador()
    {
        RuleFor(x => x.TipoContenido).Must(Tipos.Contains).WithMessage("La imagen debe ser JPG, PNG o WebP.");
        RuleFor(x => x.Contenido).Must(c => c.Length is > 0 and <= TamanoMaximo).WithMessage("La imagen debe pesar máximo 2 MB.");
    }
}

internal sealed class RechazarProductoValidador : AbstractValidator<RechazarProducto>
{
    public RechazarProductoValidador() =>
        RuleFor(x => x.Motivo).NotEmpty().WithMessage("Escribe el motivo del rechazo.").MaximumLength(500);
}

internal sealed class ProductosManejador(
    IConsultas consultas, IProductoRepositorio productos, IUnidadRepositorio unidades, IGestorUsuarios usuarios,
    IFileStorage archivos, IUnidadDeTrabajo unidad, IUsuarioActual usuario, IReloj reloj) :
    IManejador<ListarUnidades, IReadOnlyList<UnidadDto>>,
    IManejador<CrearUnidad, UnidadDto>,
    IManejador<ListarProductos, Pagina<ProductoDto>>,
    IManejador<ObtenerProducto, ProductoDto>,
    IManejador<BuscarProductosDisponibles, IReadOnlyList<ProductoOpcionDto>>,
    IManejador<CrearProducto, ProductoDto>,
    IManejador<EditarProducto, ProductoDto>,
    IManejador<CambiarEstadoProducto, ProductoDto>,
    IManejador<SubirImagenProducto, ProductoDto>,
    IManejador<AprobarProducto, ProductoDto>,
    IManejador<RechazarProducto, ProductoDto>
{
    private Auditoria Ahora => new(usuario.Id!.Value, reloj.Ahora);

    public Task<IReadOnlyList<UnidadDto>> ManejarAsync(ListarUnidades s, CancellationToken ct) => consultas.ListarUnidadesAsync(ct);

    public async Task<UnidadDto> ManejarAsync(CrearUnidad s, CancellationToken ct)
    {
        if (await unidades.ExisteClaveAsync(s.Clave.Trim().ToUpperInvariant(), ct))
            throw new DomainException($"Ya existe la unidad {s.Clave.Trim().ToUpperInvariant()}.");
        var nueva = new Unidad(s.Clave, s.Nombre, s.PermiteFracciones);
        unidades.Agregar(nueva);
        await unidad.GuardarAsync(ct);
        return new UnidadDto(nueva.Id, nueva.Clave, nueva.Nombre, nueva.PermiteFracciones);
    }

    public Task<Pagina<ProductoDto>> ManejarAsync(ListarProductos s, CancellationToken ct) =>
        consultas.ListarProductosAsync(s.Filtro, usuario.Id, ct);

    public Task<ProductoDto> ManejarAsync(ObtenerProducto s, CancellationToken ct) => Dto(s.Id, ct);

    public Task<IReadOnlyList<ProductoOpcionDto>> ManejarAsync(BuscarProductosDisponibles s, CancellationToken ct) =>
        consultas.BuscarProductosDisponiblesAsync(s.Texto, Math.Clamp(s.Maximo, 1, 50), ct);

    public async Task<ProductoDto> ManejarAsync(CrearProducto s, CancellationToken ct)
    {
        var referencia = s.Referencia.Trim().ToUpperInvariant();
        if (await productos.ExisteReferenciaAsync(referencia, ct))
            throw new DomainException($"Ya existe un producto con la referencia {referencia}.");
        var unidadMedida = await unidades.ObtenerAsync(s.UnidadId, ct) ?? throw new DomainException("La unidad no existe.");

        Producto producto;
        if (usuario.TienePermiso(Permisos.Productos.Aprobar))
        {
            producto = Producto.CrearAprobado(s.Referencia, s.Descripcion, unidadMedida, Ahora);
        }
        else
        {
            var gerenteId = s.GerenteAprobadorId ?? throw new DomainException("Elige al gerente que aprobará el producto.");
            var gerente = await usuarios.ObtenerAsync(gerenteId, ct);
            if (gerente is not { Rol: Roles.Gerente, Activo: true })
                throw new DomainException("El aprobador elegido no es un gerente activo.");
            producto = Producto.CrearPendiente(s.Referencia, s.Descripcion, unidadMedida, gerenteId, Ahora);
        }

        productos.Agregar(producto);
        await unidad.GuardarAsync(ct);
        return await Dto(producto.Id, ct);
    }

    public async Task<ProductoDto> ManejarAsync(EditarProducto s, CancellationToken ct)
    {
        var producto = await Cargar(s.Id, ct);
        var unidadMedida = await unidades.ObtenerAsync(s.UnidadId, ct) ?? throw new DomainException("La unidad no existe.");
        producto.Actualizar(s.Descripcion, unidadMedida);
        await unidad.GuardarAsync(ct);
        return await Dto(producto.Id, ct);
    }

    public async Task<ProductoDto> ManejarAsync(CambiarEstadoProducto s, CancellationToken ct)
    {
        var producto = await Cargar(s.Id, ct);
        if (s.Activo) producto.Activar(); else producto.Desactivar();
        await unidad.GuardarAsync(ct);
        return await Dto(producto.Id, ct);
    }

    public async Task<ProductoDto> ManejarAsync(SubirImagenProducto s, CancellationToken ct)
    {
        var producto = await Cargar(s.Id, ct);
        var anterior = producto.ImagenRuta;
        await using var contenido = new MemoryStream(s.Contenido);
        var ruta = await archivos.GuardarAsync(contenido, s.NombreArchivo, s.TipoContenido, ct);
        producto.AsignarImagen(ruta);
        await unidad.GuardarAsync(ct);
        if (anterior is not null)
            await archivos.EliminarAsync(anterior, ct);
        return await Dto(producto.Id, ct);
    }

    public async Task<ProductoDto> ManejarAsync(AprobarProducto s, CancellationToken ct)
    {
        var producto = await Cargar(s.Id, ct);
        producto.Aprobar(Ahora);
        await unidad.GuardarAsync(ct);
        return await Dto(producto.Id, ct);
    }

    public async Task<ProductoDto> ManejarAsync(RechazarProducto s, CancellationToken ct)
    {
        var producto = await Cargar(s.Id, ct);
        producto.Rechazar(Ahora, s.Motivo);
        await unidad.GuardarAsync(ct);
        return await Dto(producto.Id, ct);
    }

    private async Task<Producto> Cargar(Guid id, CancellationToken ct) =>
        await productos.ObtenerAsync(id, ct) ?? throw new NoEncontradoException("el producto", id);

    private async Task<ProductoDto> Dto(Guid id, CancellationToken ct) =>
        await consultas.ObtenerProductoAsync(id, ct) ?? throw new NoEncontradoException("el producto", id);
}
