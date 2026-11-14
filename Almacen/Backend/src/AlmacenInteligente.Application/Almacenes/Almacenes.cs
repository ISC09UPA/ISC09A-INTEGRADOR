using AlmacenInteligente.Application.Abstracciones;
using AlmacenInteligente.Application.CasosDeUso;
using AlmacenInteligente.Application.Consultas;
using AlmacenInteligente.Application.Seguridad;
using AlmacenInteligente.Application.Terceros;
using AlmacenInteligente.Domain.Common;
using AlmacenInteligente.Domain.Inventario;
using FluentValidation;
using Almacen = AlmacenInteligente.Domain.Almacenes.Almacen;

namespace AlmacenInteligente.Application.Almacenes;

[RequierePermiso(Permisos.Almacenes.Ver)]
public sealed record ListarAlmacenes(bool IncluirInactivos = false) : ISolicitud<IReadOnlyList<AlmacenDto>>;

[RequierePermiso(Permisos.Almacenes.Gestionar)]
public sealed record CrearAlmacen(string Nombre, string Ubicacion) : ISolicitud<AlmacenDto>;

[RequierePermiso(Permisos.Almacenes.Gestionar)]
public sealed record EditarAlmacen(Guid Id, string Nombre, string Ubicacion) : ISolicitud<AlmacenDto>;

[RequierePermiso(Permisos.Almacenes.Gestionar)]
public sealed record CambiarEstadoAlmacen(Guid Id, bool Activo) : ISolicitud<AlmacenDto>;

[RequierePermiso(Permisos.Almacenes.VerExistencias)]
public sealed record ListarExistencias(FiltroExistencias Filtro) : ISolicitud<Pagina<ExistenciaDto>>;

[RequierePermiso(Permisos.Almacenes.VerExistencias)]
public sealed record ConsultarKardex(Guid AlmacenId, Guid ProductoId, DateOnly? Desde = null, DateOnly? Hasta = null)
    : ISolicitud<IReadOnlyList<KardexDto>>;

[RequierePermiso(Permisos.Almacenes.Gestionar)]
public sealed record DefinirStockMinimo(Guid AlmacenId, Guid ProductoId, decimal Minimo) : ISolicitud<bool>;

/// <summary>Carga existencia que ya había antes de usar el sistema; queda en el kardex como movimiento inicial.</summary>
[RequierePermiso(Permisos.Almacenes.Gestionar)]
public sealed record RegistrarStockInicial(Guid AlmacenId, Guid ProductoId, decimal Cantidad, decimal CostoUnitario) : ISolicitud<bool>;

internal sealed class CrearAlmacenValidador : AbstractValidator<CrearAlmacen>
{
    public CrearAlmacenValidador()
    {
        RuleFor(x => x.Nombre).NotEmpty().WithMessage("El nombre es obligatorio.").MaximumLength(120);
        RuleFor(x => x.Ubicacion).NotEmpty().WithMessage("La ubicación es obligatoria.").MaximumLength(300);
    }
}

internal sealed class EditarAlmacenValidador : AbstractValidator<EditarAlmacen>
{
    public EditarAlmacenValidador()
    {
        RuleFor(x => x.Nombre).NotEmpty().WithMessage("El nombre es obligatorio.").MaximumLength(120);
        RuleFor(x => x.Ubicacion).NotEmpty().WithMessage("La ubicación es obligatoria.").MaximumLength(300);
    }
}

internal sealed class StockInicialValidador : AbstractValidator<RegistrarStockInicial>
{
    public StockInicialValidador()
    {
        RuleFor(x => x.Cantidad).GreaterThan(0).WithMessage("La cantidad debe ser mayor que cero.");
        RuleFor(x => x.CostoUnitario).GreaterThanOrEqualTo(0).WithMessage("El costo no puede ser negativo.");
    }
}

internal sealed class AlmacenesManejador(
    IConsultas consultas, IAlmacenRepositorio almacenes, IProductoRepositorio productos, IExistenciaRepositorio existencias,
    IUnidadDeTrabajo unidad, IUsuarioActual usuario, IReloj reloj) :
    IManejador<ListarAlmacenes, IReadOnlyList<AlmacenDto>>,
    IManejador<CrearAlmacen, AlmacenDto>,
    IManejador<EditarAlmacen, AlmacenDto>,
    IManejador<CambiarEstadoAlmacen, AlmacenDto>,
    IManejador<ListarExistencias, Pagina<ExistenciaDto>>,
    IManejador<ConsultarKardex, IReadOnlyList<KardexDto>>,
    IManejador<DefinirStockMinimo, bool>,
    IManejador<RegistrarStockInicial, bool>
{
    public Task<IReadOnlyList<AlmacenDto>> ManejarAsync(ListarAlmacenes s, CancellationToken ct) =>
        consultas.ListarAlmacenesAsync(s.IncluirInactivos, ct);

    public async Task<AlmacenDto> ManejarAsync(CrearAlmacen s, CancellationToken ct)
    {
        if (await almacenes.ExisteNombreAsync(s.Nombre.Trim(), null, ct))
            throw new DomainException($"Ya existe un almacén llamado {s.Nombre.Trim()}.");
        var almacen = new Almacen(s.Nombre, s.Ubicacion, new Auditoria(usuario.Id!.Value, reloj.Ahora));
        almacenes.Agregar(almacen);
        await unidad.GuardarAsync(ct);
        return await Dto(almacen.Id, ct);
    }

    public async Task<AlmacenDto> ManejarAsync(EditarAlmacen s, CancellationToken ct)
    {
        var almacen = await Cargar(s.Id, ct);
        if (await almacenes.ExisteNombreAsync(s.Nombre.Trim(), s.Id, ct))
            throw new DomainException($"Ya existe un almacén llamado {s.Nombre.Trim()}.");
        almacen.Actualizar(s.Nombre, s.Ubicacion);
        await unidad.GuardarAsync(ct);
        return await Dto(almacen.Id, ct);
    }

    public async Task<AlmacenDto> ManejarAsync(CambiarEstadoAlmacen s, CancellationToken ct)
    {
        var almacen = await Cargar(s.Id, ct);
        if (s.Activo) almacen.Activar(); else almacen.Desactivar();
        await unidad.GuardarAsync(ct);
        return await Dto(almacen.Id, ct);
    }

    public Task<Pagina<ExistenciaDto>> ManejarAsync(ListarExistencias s, CancellationToken ct) =>
        consultas.ListarExistenciasAsync(s.Filtro, ct);

    public Task<IReadOnlyList<KardexDto>> ManejarAsync(ConsultarKardex s, CancellationToken ct) =>
        consultas.KardexAsync(s.AlmacenId, s.ProductoId, s.Desde, s.Hasta, ct);

    public async Task<bool> ManejarAsync(DefinirStockMinimo s, CancellationToken ct)
    {
        var lote = await existencias.CargarLoteAsync([(s.AlmacenId, s.ProductoId)], ct);
        await AsegurarAlmacenYProducto(s.AlmacenId, s.ProductoId, ct);
        lote.ParaEntrada(s.AlmacenId, s.ProductoId).DefinirStockMinimo(s.Minimo, reloj.Ahora);
        existencias.Registrar(lote, new ResultadoInventario([], null));
        await unidad.GuardarAsync(ct);
        return true;
    }

    public async Task<bool> ManejarAsync(RegistrarStockInicial s, CancellationToken ct)
    {
        await AsegurarAlmacenYProducto(s.AlmacenId, s.ProductoId, ct);
        var lote = await existencias.CargarLoteAsync([(s.AlmacenId, s.ProductoId)], ct);
        var resultado = ServicioInventario.RegistrarStockInicial(
            lote.ParaEntrada(s.AlmacenId, s.ProductoId), s.Cantidad, s.CostoUnitario, new Auditoria(usuario.Id!.Value, reloj.Ahora));
        existencias.Registrar(lote, resultado);
        await unidad.GuardarAsync(ct);
        return true;
    }

    private async Task AsegurarAlmacenYProducto(Guid almacenId, Guid productoId, CancellationToken ct)
    {
        var almacen = await Cargar(almacenId, ct);
        if (!almacen.Activo)
            throw new DomainException($"El almacén {almacen.Nombre} está inactivo.");
        var producto = await productos.ObtenerAsync(productoId, ct) ?? throw new NoEncontradoException("el producto", productoId);
        if (!producto.EstaDisponible)
            throw new DomainException($"El producto {producto.Referencia} no está aprobado o está inactivo.");
    }

    private async Task<Almacen> Cargar(Guid id, CancellationToken ct) =>
        await almacenes.ObtenerAsync(id, ct) ?? throw new NoEncontradoException("el almacén", id);

    private async Task<AlmacenDto> Dto(Guid id, CancellationToken ct) =>
        (await consultas.ListarAlmacenesAsync(true, ct)).Single(a => a.Id == id);
}
