using AlmacenInteligente.Application.Abstracciones;
using AlmacenInteligente.Application.CasosDeUso;
using AlmacenInteligente.Application.Consultas;
using AlmacenInteligente.Application.Seguridad;
using AlmacenInteligente.Application.Terceros;
using AlmacenInteligente.Domain.Common;
using AlmacenInteligente.Domain.Inventario;
using AlmacenInteligente.Domain.Productos;
using AlmacenInteligente.Domain.Recepciones;
using AlmacenInteligente.Domain.ValueObjects;
using AlmacenInteligente.Domain.Ventas;
using FluentValidation;

namespace AlmacenInteligente.Application.Documentos;

// Recepciones, ventas y traspasos se registran ya confirmados: se capturan completos en pantalla
// y se guardan en una sola transacción con sus existencias, kardex y movimiento financiero.

public sealed record LineaRecepcionDto(Guid ProductoId, decimal Cantidad, decimal CostoUnitario, decimal TasaIva);

public sealed record LineaVentaDto(Guid ProductoId, Guid AlmacenId, decimal Cantidad, decimal PrecioUnitario, decimal TasaIva);

public sealed record LineaTraspasoDto(Guid ProductoId, decimal Cantidad);

/// <summary>Resultado de registrar un documento: su id y folio para mostrarlo o abrirlo.</summary>
public sealed record DocumentoRegistradoDto(Guid Id, long Folio);

[RequierePermiso(Permisos.Proveedores.Ver)]
public sealed record BuscarProveedoresActivos(string? Texto, int Maximo = 20) : ISolicitud<IReadOnlyList<OpcionDto>>;

[RequierePermiso(Permisos.Clientes.Ver)]
public sealed record BuscarClientesActivos(string? Texto, int Maximo = 20) : ISolicitud<IReadOnlyList<OpcionDto>>;

[RequierePermiso(Permisos.Recepciones.Ver)]
public sealed record ListarRecepciones(FiltroDocumentos Filtro) : ISolicitud<Pagina<DocumentoResumenDto>>;

[RequierePermiso(Permisos.Recepciones.Ver)]
public sealed record ObtenerRecepcion(Guid Id) : ISolicitud<DocumentoDetalleDto>;

[RequierePermiso(Permisos.Recepciones.Crear)]
public sealed record RegistrarRecepcion(Guid ProveedorId, Guid AlmacenId, DateOnly Fecha, IReadOnlyList<LineaRecepcionDto> Lineas)
    : ISolicitud<DocumentoRegistradoDto>;

[RequierePermiso(Permisos.Recepciones.Cancelar)]
public sealed record CancelarRecepcion(Guid Id, string Motivo) : ISolicitud<bool>;

[RequierePermiso(Permisos.Ventas.Ver)]
public sealed record ListarVentas(FiltroDocumentos Filtro) : ISolicitud<Pagina<DocumentoResumenDto>>;

[RequierePermiso(Permisos.Ventas.Ver)]
public sealed record ObtenerVenta(Guid Id) : ISolicitud<DocumentoDetalleDto>;

[RequierePermiso(Permisos.Ventas.Crear)]
public sealed record RegistrarVenta(Guid ClienteId, DateOnly Fecha, IReadOnlyList<LineaVentaDto> Lineas)
    : ISolicitud<DocumentoRegistradoDto>;

[RequierePermiso(Permisos.Ventas.Cancelar)]
public sealed record CancelarVenta(Guid Id, string Motivo) : ISolicitud<bool>;

[RequierePermiso(Permisos.Traspasos.Ver)]
public sealed record ListarTraspasos(FiltroDocumentos Filtro) : ISolicitud<Pagina<DocumentoResumenDto>>;

[RequierePermiso(Permisos.Traspasos.Ver)]
public sealed record ObtenerTraspaso(Guid Id) : ISolicitud<DocumentoDetalleDto>;

[RequierePermiso(Permisos.Traspasos.Crear)]
public sealed record RegistrarTraspaso(Guid AlmacenOrigenId, Guid AlmacenDestinoId, DateOnly Fecha, IReadOnlyList<LineaTraspasoDto> Lineas)
    : ISolicitud<DocumentoRegistradoDto>;

internal sealed class RegistrarRecepcionValidador : AbstractValidator<RegistrarRecepcion>
{
    public RegistrarRecepcionValidador()
    {
        RuleFor(x => x.ProveedorId).NotEmpty().WithMessage("Elige el proveedor.");
        RuleFor(x => x.AlmacenId).NotEmpty().WithMessage("Elige el almacén de destino.");
        RuleFor(x => x.Lineas).NotEmpty().WithMessage("Agrega al menos un producto.");
        RuleForEach(x => x.Lineas).ChildRules(l =>
        {
            l.RuleFor(x => x.ProductoId).NotEmpty().WithMessage("Hay una línea sin producto.");
            l.RuleFor(x => x.Cantidad).GreaterThan(0).WithMessage("Las cantidades deben ser mayores que cero.");
            l.RuleFor(x => x.CostoUnitario).GreaterThanOrEqualTo(0).WithMessage("Los costos no pueden ser negativos.");
        });
    }
}

internal sealed class RegistrarVentaValidador : AbstractValidator<RegistrarVenta>
{
    public RegistrarVentaValidador()
    {
        RuleFor(x => x.ClienteId).NotEmpty().WithMessage("Elige el cliente.");
        RuleFor(x => x.Lineas).NotEmpty().WithMessage("Agrega al menos un producto.");
        RuleForEach(x => x.Lineas).ChildRules(l =>
        {
            l.RuleFor(x => x.ProductoId).NotEmpty().WithMessage("Hay una línea sin producto.");
            l.RuleFor(x => x.AlmacenId).NotEmpty().WithMessage("Elige el almacén de salida de cada línea.");
            l.RuleFor(x => x.Cantidad).GreaterThan(0).WithMessage("Las cantidades deben ser mayores que cero.");
            l.RuleFor(x => x.PrecioUnitario).GreaterThanOrEqualTo(0).WithMessage("Los precios no pueden ser negativos.");
        });
    }
}

internal sealed class RegistrarTraspasoValidador : AbstractValidator<RegistrarTraspaso>
{
    public RegistrarTraspasoValidador()
    {
        RuleFor(x => x.AlmacenOrigenId).NotEmpty().WithMessage("Elige el almacén de origen.");
        RuleFor(x => x.AlmacenDestinoId).NotEmpty().WithMessage("Elige el almacén de destino.");
        RuleFor(x => x.Lineas).NotEmpty().WithMessage("Agrega al menos un producto.");
        RuleForEach(x => x.Lineas).ChildRules(l =>
        {
            l.RuleFor(x => x.ProductoId).NotEmpty().WithMessage("Hay una línea sin producto.");
            l.RuleFor(x => x.Cantidad).GreaterThan(0).WithMessage("Las cantidades deben ser mayores que cero.");
        });
    }
}

internal sealed class CancelarRecepcionValidador : AbstractValidator<CancelarRecepcion>
{
    public CancelarRecepcionValidador() => RuleFor(x => x.Motivo).NotEmpty().WithMessage("Escribe el motivo de la cancelación.");
}

internal sealed class CancelarVentaValidador : AbstractValidator<CancelarVenta>
{
    public CancelarVentaValidador() => RuleFor(x => x.Motivo).NotEmpty().WithMessage("Escribe el motivo de la cancelación.");
}

internal sealed class DocumentosManejador(
    IConsultas consultas,
    IClienteRepositorio clientes, IProveedorRepositorio proveedores, IAlmacenRepositorio almacenes,
    IProductoRepositorio productos, IExistenciaRepositorio existencias,
    IRecepcionRepositorio recepciones, IVentaRepositorio ventas, ITraspasoRepositorio traspasos,
    IUnidadDeTrabajo unidad, IUsuarioActual usuario, IReloj reloj) :
    IManejador<BuscarProveedoresActivos, IReadOnlyList<OpcionDto>>,
    IManejador<BuscarClientesActivos, IReadOnlyList<OpcionDto>>,
    IManejador<ListarRecepciones, Pagina<DocumentoResumenDto>>,
    IManejador<ObtenerRecepcion, DocumentoDetalleDto>,
    IManejador<RegistrarRecepcion, DocumentoRegistradoDto>,
    IManejador<CancelarRecepcion, bool>,
    IManejador<ListarVentas, Pagina<DocumentoResumenDto>>,
    IManejador<ObtenerVenta, DocumentoDetalleDto>,
    IManejador<RegistrarVenta, DocumentoRegistradoDto>,
    IManejador<CancelarVenta, bool>,
    IManejador<ListarTraspasos, Pagina<DocumentoResumenDto>>,
    IManejador<ObtenerTraspaso, DocumentoDetalleDto>,
    IManejador<RegistrarTraspaso, DocumentoRegistradoDto>
{
    private Auditoria Ahora => new(usuario.Id!.Value, reloj.Ahora);

    public Task<IReadOnlyList<OpcionDto>> ManejarAsync(BuscarProveedoresActivos s, CancellationToken ct) =>
        consultas.BuscarTercerosActivosAsync(clientes: false, s.Texto, Math.Clamp(s.Maximo, 1, 50), ct);

    public Task<IReadOnlyList<OpcionDto>> ManejarAsync(BuscarClientesActivos s, CancellationToken ct) =>
        consultas.BuscarTercerosActivosAsync(clientes: true, s.Texto, Math.Clamp(s.Maximo, 1, 50), ct);

    // ---- Recepciones

    public Task<Pagina<DocumentoResumenDto>> ManejarAsync(ListarRecepciones s, CancellationToken ct) =>
        consultas.ListarRecepcionesAsync(s.Filtro, ct);

    public async Task<DocumentoDetalleDto> ManejarAsync(ObtenerRecepcion s, CancellationToken ct) =>
        await consultas.ObtenerRecepcionAsync(s.Id, ct) ?? throw new NoEncontradoException("la recepción", s.Id);

    public async Task<DocumentoRegistradoDto> ManejarAsync(RegistrarRecepcion s, CancellationToken ct)
    {
        var proveedor = await proveedores.ObtenerAsync(s.ProveedorId, ct) ?? throw new NoEncontradoException("el proveedor", s.ProveedorId);
        var almacen = await almacenes.ObtenerAsync(s.AlmacenId, ct) ?? throw new NoEncontradoException("el almacén", s.AlmacenId);
        var catalogo = await Productos(s.Lineas.Select(l => l.ProductoId), ct);

        var recepcion = new Recepcion(proveedor, almacen, s.Fecha, Ahora);
        foreach (var l in s.Lineas)
            recepcion.AgregarLinea(catalogo[l.ProductoId], l.Cantidad, l.CostoUnitario, new TasaIva(l.TasaIva));

        var lote = await existencias.CargarLoteAsync(s.Lineas.Select(l => (s.AlmacenId, l.ProductoId)), ct);
        var resultado = ServicioInventario.ConfirmarRecepcion(recepcion, lote, Ahora);
        recepciones.Agregar(recepcion);
        existencias.Registrar(lote, resultado);
        await unidad.GuardarAsync(ct);
        return new DocumentoRegistradoDto(recepcion.Id, recepcion.Folio);
    }

    public async Task<bool> ManejarAsync(CancelarRecepcion s, CancellationToken ct)
    {
        var recepcion = await recepciones.ObtenerAsync(s.Id, ct) ?? throw new NoEncontradoException("la recepción", s.Id);
        var lote = await existencias.CargarLoteAsync(recepcion.Lineas.Select(l => (recepcion.AlmacenId, l.ProductoId)), ct);
        var resultado = ServicioInventario.CancelarRecepcion(recepcion, lote, Ahora, s.Motivo);
        existencias.Registrar(lote, resultado);
        await unidad.GuardarAsync(ct);
        return true;
    }

    // ---- Ventas

    public Task<Pagina<DocumentoResumenDto>> ManejarAsync(ListarVentas s, CancellationToken ct) =>
        consultas.ListarVentasAsync(s.Filtro, ct);

    public async Task<DocumentoDetalleDto> ManejarAsync(ObtenerVenta s, CancellationToken ct) =>
        await consultas.ObtenerVentaAsync(s.Id, ct) ?? throw new NoEncontradoException("la venta", s.Id);

    public async Task<DocumentoRegistradoDto> ManejarAsync(RegistrarVenta s, CancellationToken ct)
    {
        var cliente = await clientes.ObtenerAsync(s.ClienteId, ct) ?? throw new NoEncontradoException("el cliente", s.ClienteId);
        var catalogo = await Productos(s.Lineas.Select(l => l.ProductoId), ct);
        foreach (var almacenId in s.Lineas.Select(l => l.AlmacenId).Distinct())
            await AlmacenActivo(almacenId, ct);

        var venta = new Venta(cliente, s.Fecha, Ahora);
        foreach (var l in s.Lineas)
            venta.AgregarLinea(catalogo[l.ProductoId], l.AlmacenId, l.Cantidad, l.PrecioUnitario, new TasaIva(l.TasaIva));

        var lote = await existencias.CargarLoteAsync(s.Lineas.Select(l => (l.AlmacenId, l.ProductoId)), ct);
        var resultado = ServicioInventario.ConfirmarVenta(venta, lote, Ahora);
        ventas.Agregar(venta);
        existencias.Registrar(lote, resultado);
        await unidad.GuardarAsync(ct);
        return new DocumentoRegistradoDto(venta.Id, venta.Folio);
    }

    public async Task<bool> ManejarAsync(CancelarVenta s, CancellationToken ct)
    {
        var venta = await ventas.ObtenerAsync(s.Id, ct) ?? throw new NoEncontradoException("la venta", s.Id);
        var lote = await existencias.CargarLoteAsync(venta.Lineas.Select(l => (l.AlmacenId, l.ProductoId)), ct);
        var resultado = ServicioInventario.CancelarVenta(venta, lote, Ahora, s.Motivo);
        existencias.Registrar(lote, resultado);
        await unidad.GuardarAsync(ct);
        return true;
    }

    // ---- Traspasos

    public Task<Pagina<DocumentoResumenDto>> ManejarAsync(ListarTraspasos s, CancellationToken ct) =>
        consultas.ListarTraspasosAsync(s.Filtro, ct);

    public async Task<DocumentoDetalleDto> ManejarAsync(ObtenerTraspaso s, CancellationToken ct) =>
        await consultas.ObtenerTraspasoAsync(s.Id, ct) ?? throw new NoEncontradoException("el traspaso", s.Id);

    public async Task<DocumentoRegistradoDto> ManejarAsync(RegistrarTraspaso s, CancellationToken ct)
    {
        var origen = await almacenes.ObtenerAsync(s.AlmacenOrigenId, ct) ?? throw new NoEncontradoException("el almacén", s.AlmacenOrigenId);
        var destino = await almacenes.ObtenerAsync(s.AlmacenDestinoId, ct) ?? throw new NoEncontradoException("el almacén", s.AlmacenDestinoId);
        var catalogo = await Productos(s.Lineas.Select(l => l.ProductoId), ct);

        var traspaso = new Traspaso(origen, destino, s.Fecha, Ahora);
        foreach (var l in s.Lineas)
            traspaso.AgregarLinea(catalogo[l.ProductoId], l.Cantidad);

        var claves = s.Lineas.SelectMany(l => new[] { (s.AlmacenOrigenId, l.ProductoId), (s.AlmacenDestinoId, l.ProductoId) });
        var lote = await existencias.CargarLoteAsync(claves, ct);
        var resultado = ServicioInventario.ConfirmarTraspaso(traspaso, lote, Ahora);
        traspasos.Agregar(traspaso);
        existencias.Registrar(lote, resultado);
        await unidad.GuardarAsync(ct);
        return new DocumentoRegistradoDto(traspaso.Id, traspaso.Folio);
    }

    private async Task<IReadOnlyDictionary<Guid, Producto>> Productos(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var lista = ids.Distinct().ToList();
        var encontrados = await productos.ObtenerVariosAsync(lista, ct);
        if (encontrados.Count != lista.Count)
            throw new DomainException("Uno de los productos ya no existe.");
        return encontrados;
    }

    private async Task AlmacenActivo(Guid id, CancellationToken ct)
    {
        var almacen = await almacenes.ObtenerAsync(id, ct) ?? throw new NoEncontradoException("el almacén", id);
        if (!almacen.Activo)
            throw new DomainException($"El almacén {almacen.Nombre} está inactivo.");
    }
}
