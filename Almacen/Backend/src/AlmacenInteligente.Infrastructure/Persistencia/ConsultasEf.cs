using System.Globalization;
using AlmacenInteligente.Application.Consultas;
using AlmacenInteligente.Application.Terceros;
using AlmacenInteligente.Domain.Documentos;
using AlmacenInteligente.Domain.Finanzas;
using AlmacenInteligente.Domain.Inventario;
using AlmacenInteligente.Domain.Productos;
using AlmacenInteligente.Domain.Recepciones;
using AlmacenInteligente.Domain.Ventas;
using Microsoft.EntityFrameworkCore;

namespace AlmacenInteligente.Infrastructure.Persistencia;

/// <summary>
/// Lecturas para pantallas y reportes. Los importes (Dinero) se suman en memoria porque son value objects
/// con conversión; los volúmenes de una página o de un periodo de reporte lo permiten.
/// </summary>
internal sealed class ConsultasEf(AlmacenDbContext db) : IConsultas
{
    private static readonly CultureInfo Mx = CultureInfo.GetCultureInfo("es-MX");
    private static readonly TimeZoneInfo ZonaMexico = TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");

    // ---------------- Catálogos

    public async Task<IReadOnlyList<UnidadDto>> ListarUnidadesAsync(CancellationToken ct) =>
        await db.Unidades.AsNoTracking().OrderBy(u => u.Nombre)
            .Select(u => new UnidadDto(u.Id, u.Clave, u.Nombre, u.PermiteFracciones))
            .ToListAsync(ct);

    public async Task<Pagina<ProductoDto>> ListarProductosAsync(FiltroProductos f, Guid? usuarioId, CancellationToken ct)
    {
        var q = db.Productos.AsNoTracking();
        if (!f.IncluirInactivos)
            q = q.Where(p => p.Activo);
        if (f.Estado is { } estado)
            q = q.Where(p => p.Estado == estado);
        if (f.SoloMisAprobaciones)
            q = q.Where(p => p.GerenteAprobadorId == usuarioId);
        if (!string.IsNullOrWhiteSpace(f.Busqueda))
        {
            var patron = $"%{f.Busqueda.Trim()}%";
            q = q.Where(p => EF.Functions.ILike(p.Referencia, patron) || EF.Functions.ILike(p.Descripcion, patron));
        }

        var total = await q.CountAsync(ct);
        var pagina = q.OrderBy(p => p.Referencia).Skip((Math.Max(f.NumeroPagina, 1) - 1) * f.Tamano).Take(Math.Clamp(f.Tamano, 1, 200));
        return new Pagina<ProductoDto>(await ProyectarProductos(pagina, ct), total, f.NumeroPagina, f.Tamano);
    }

    public async Task<ProductoDto?> ObtenerProductoAsync(Guid id, CancellationToken ct) =>
        (await ProyectarProductos(db.Productos.AsNoTracking().Where(p => p.Id == id), ct)).SingleOrDefault();

    private async Task<IReadOnlyList<ProductoDto>> ProyectarProductos(IQueryable<Producto> q, CancellationToken ct)
    {
        var filas = await q.Select(p => new
        {
            Producto = p,
            Unidad = db.Unidades.Where(u => u.Id == p.UnidadId).Select(u => u.Clave).First(),
            Existencia = db.Existencias.Where(e => e.ProductoId == p.Id).Sum(e => (decimal?)e.Cantidad) ?? 0,
        }).ToListAsync(ct);

        var nombres = await NombresUsuarios(filas.SelectMany(f => new[] { f.Producto.Creado.UsuarioId, f.Producto.GerenteAprobadorId ?? Guid.Empty }), ct);
        return filas.Select(f =>
        {
            var p = f.Producto;
            return new ProductoDto(p.Id, p.Referencia, p.Descripcion, p.UnidadId, f.Unidad, p.PermiteFracciones, p.ImagenRuta,
                p.Activo, p.Estado, p.GerenteAprobadorId, p.GerenteAprobadorId is { } g ? nombres.GetValueOrDefault(g) : null,
                nombres.GetValueOrDefault(p.Creado.UsuarioId, "—"), p.Creado.Fecha, p.MotivoRechazo, f.Existencia);
        }).ToList();
    }

    public async Task<IReadOnlyList<ProductoOpcionDto>> BuscarProductosDisponiblesAsync(string? texto, int maximo, CancellationToken ct)
    {
        var q = db.Productos.AsNoTracking().Where(p => p.Activo && p.Estado == EstadoAprobacion.Aprobado);
        if (!string.IsNullOrWhiteSpace(texto))
        {
            var patron = $"%{texto.Trim()}%";
            q = q.Where(p => EF.Functions.ILike(p.Referencia, patron) || EF.Functions.ILike(p.Descripcion, patron));
        }

        var productos = await q.OrderBy(p => p.Referencia).Take(maximo)
            .Select(p => new
            {
                p.Id, p.Referencia, p.Descripcion, p.PermiteFracciones,
                Unidad = db.Unidades.Where(u => u.Id == p.UnidadId).Select(u => u.Clave).First(),
            }).ToListAsync(ct);

        var ids = productos.Select(p => p.Id).ToList();
        var existencias = await (
            from e in db.Existencias.AsNoTracking()
            join a in db.Almacenes on e.AlmacenId equals a.Id
            where ids.Contains(e.ProductoId) && a.Activo
            orderby a.Nombre
            select new { e.ProductoId, Opcion = new ExistenciaOpcionDto(a.Id, a.Nombre, e.Cantidad, e.CostoPromedio) }
        ).ToListAsync(ct);
        var porProducto = existencias.ToLookup(e => e.ProductoId, e => e.Opcion);

        return productos.Select(p => new ProductoOpcionDto(p.Id, p.Referencia, p.Descripcion, p.Unidad, p.PermiteFracciones,
            porProducto[p.Id].ToList())).ToList();
    }

    public async Task<IReadOnlyList<AlmacenDto>> ListarAlmacenesAsync(bool incluirInactivos, CancellationToken ct)
    {
        var filas = await db.Almacenes.AsNoTracking()
            .Where(a => incluirInactivos || a.Activo)
            .OrderBy(a => a.Nombre)
            .Select(a => new
            {
                a.Id, a.Nombre, a.Ubicacion, a.Activo,
                Productos = db.Existencias.Count(e => e.AlmacenId == a.Id && e.Cantidad > 0),
                Valor = db.Existencias.Where(e => e.AlmacenId == a.Id).Sum(e => (decimal?)(e.Cantidad * e.CostoPromedio)) ?? 0,
            }).ToListAsync(ct);
        return filas.Select(a => new AlmacenDto(a.Id, a.Nombre, a.Ubicacion, a.Activo, a.Productos, Redondear(a.Valor))).ToList();
    }

    public async Task<IReadOnlyList<OpcionDto>> BuscarTercerosActivosAsync(bool clientes, string? texto, int maximo, CancellationToken ct)
    {
        var patron = string.IsNullOrWhiteSpace(texto) ? null : $"%{texto.Trim()}%";
        if (clientes)
            return await db.Clientes.AsNoTracking()
                .Where(t => t.Activo && (patron == null || EF.Functions.ILike(t.Nombre, patron) || EF.Functions.ILike(t.Codigo, patron)
                    || EF.Functions.ILike((string)(object)t.Rfc, patron)))
                .OrderBy(t => t.Nombre).Take(maximo)
                .Select(t => new OpcionDto(t.Id, t.Codigo + " · " + t.Nombre)).ToListAsync(ct);
        return await db.Proveedores.AsNoTracking()
            .Where(t => t.Activo && (patron == null || EF.Functions.ILike(t.Nombre, patron) || EF.Functions.ILike(t.Codigo, patron)
                || EF.Functions.ILike((string)(object)t.Rfc, patron)))
            .OrderBy(t => t.Nombre).Take(maximo)
            .Select(t => new OpcionDto(t.Id, t.Codigo + " · " + t.Nombre)).ToListAsync(ct);
    }

    // ---------------- Existencias

    public async Task<Pagina<ExistenciaDto>> ListarExistenciasAsync(FiltroExistencias f, CancellationToken ct)
    {
        var q = ConsultaExistencias();
        if (f.AlmacenId is { } almacenId)
            q = q.Where(x => x.E.AlmacenId == almacenId);
        if (!string.IsNullOrWhiteSpace(f.Busqueda))
        {
            var patron = $"%{f.Busqueda.Trim()}%";
            q = q.Where(x => EF.Functions.ILike(x.P.Referencia, patron) || EF.Functions.ILike(x.P.Descripcion, patron));
        }
        if (f.SoloBajoMinimo)
            q = q.Where(x => x.E.StockMinimo > 0 && x.E.Cantidad <= x.E.StockMinimo);

        var total = await q.CountAsync(ct);
        var filas = await q.OrderBy(x => x.A.Nombre).ThenBy(x => x.P.Referencia)
            .Skip((Math.Max(f.NumeroPagina, 1) - 1) * f.Tamano).Take(Math.Clamp(f.Tamano, 1, 500))
            .ToListAsync(ct);
        return new Pagina<ExistenciaDto>(filas.Select(ADto).ToList(), total, f.NumeroPagina, f.Tamano);
    }

    /// <summary>Clase con init (no record posicional) para que EF pueda seguir filtrando sobre la proyección.</summary>
    private sealed class FilaExistencia
    {
        public Existencia E { get; init; } = null!;
        public Domain.Almacenes.Almacen A { get; init; } = null!;
        public Producto P { get; init; } = null!;
        public string Unidad { get; init; } = string.Empty;
    }

    private IQueryable<FilaExistencia> ConsultaExistencias() =>
        from e in db.Existencias.AsNoTracking()
        join a in db.Almacenes on e.AlmacenId equals a.Id
        join p in db.Productos on e.ProductoId equals p.Id
        join u in db.Unidades on p.UnidadId equals u.Id
        select new FilaExistencia { E = e, A = a, P = p, Unidad = u.Clave };

    private static ExistenciaDto ADto(FilaExistencia x) =>
        new(x.A.Id, x.A.Nombre, x.P.Id, x.P.Referencia, x.P.Descripcion, x.Unidad, x.P.PermiteFracciones,
            x.E.Cantidad, x.E.StockMinimo, x.E.CostoPromedio, Redondear(x.E.Cantidad * x.E.CostoPromedio), x.E.EstaBajoMinimo);

    public async Task<IReadOnlyList<KardexDto>> KardexAsync(Guid almacenId, Guid productoId, DateOnly? desde, DateOnly? hasta, CancellationToken ct)
    {
        var q = db.Kardex.AsNoTracking().Where(m => m.AlmacenId == almacenId && m.ProductoId == productoId);
        if (desde is { } d)
        {
            var inicio = InicioDia(d);
            q = q.Where(m => m.Registro.Fecha >= inicio);
        }
        if (hasta is { } h)
        {
            var fin = InicioDia(h.AddDays(1));
            q = q.Where(m => m.Registro.Fecha < fin);
        }

        var movimientos = await q.OrderBy(m => m.Registro.Fecha).ThenBy(m => m.Id).Take(2000).ToListAsync(ct);
        var folios = await Folios(movimientos.Where(m => m.Documento is not null).Select(m => m.Documento!), ct);
        var nombres = await NombresUsuarios(movimientos.Select(m => m.Registro.UsuarioId), ct);

        return movimientos.Select(m => new KardexDto(
            m.Registro.Fecha, m.Tipo, m.Cantidad, m.CostoUnitario, m.SaldoCantidad, m.CostoPromedio,
            m.Documento?.Tipo, m.Documento?.Id, m.Documento is { } doc ? folios.GetValueOrDefault(doc.Id) : null,
            nombres.GetValueOrDefault(m.Registro.UsuarioId, "—"))).ToList();
    }

    // ---------------- Documentos

    public async Task<Pagina<DocumentoResumenDto>> ListarRecepcionesAsync(FiltroDocumentos f, CancellationToken ct)
    {
        var q = from r in FiltrarDocumentos(db.Recepciones.AsNoTracking().IgnoreAutoIncludes(), f)
                join t in db.Proveedores on r.ProveedorId equals t.Id
                join a in db.Almacenes on r.AlmacenId equals a.Id
                select new { Doc = r, Tercero = t.Nombre, Almacen = a.Nombre };
        if (BuscarTexto(f) is { } patron)
            q = q.Where(x => EF.Functions.ILike(x.Tercero, patron) || x.Doc.Folio.ToString() == f.Busqueda!.Trim());

        var total = await q.CountAsync(ct);
        var filas = await q.OrderByDescending(x => x.Doc.Folio).Skip(Saltar(f)).Take(f.Tamano).ToListAsync(ct);
        var ids = filas.Select(x => x.Doc.Id).ToList();
        var lineas = (await db.Set<LineaRecepcion>().AsNoTracking()
                .Where(l => ids.Contains(EF.Property<Guid>(l, "RecepcionId")))
                .Select(l => new { DocId = EF.Property<Guid>(l, "RecepcionId"), l.Importe })
                .ToListAsync(ct))
            .ToLookup(l => l.DocId, l => l.Importe);

        return new Pagina<DocumentoResumenDto>(filas.Select(x => Resumen(x.Doc, x.Tercero, x.Almacen, lineas[x.Doc.Id])).ToList(),
            total, f.NumeroPagina, f.Tamano);
    }

    public async Task<Pagina<DocumentoResumenDto>> ListarVentasAsync(FiltroDocumentos f, CancellationToken ct)
    {
        var q = from v in FiltrarDocumentos(db.Ventas.AsNoTracking().IgnoreAutoIncludes(), f)
                join t in db.Clientes on v.ClienteId equals t.Id
                select new { Doc = v, Tercero = t.Nombre };
        if (BuscarTexto(f) is { } patron)
            q = q.Where(x => EF.Functions.ILike(x.Tercero, patron) || x.Doc.Folio.ToString() == f.Busqueda!.Trim());

        var total = await q.CountAsync(ct);
        var filas = await q.OrderByDescending(x => x.Doc.Folio).Skip(Saltar(f)).Take(f.Tamano).ToListAsync(ct);
        var ids = filas.Select(x => x.Doc.Id).ToList();
        var lineas = await (
            from l in db.Set<LineaVenta>().AsNoTracking()
            join a in db.Almacenes on l.AlmacenId equals a.Id
            where ids.Contains(EF.Property<Guid>(l, "VentaId"))
            select new { DocId = EF.Property<Guid>(l, "VentaId"), l.Importe, Almacen = a.Nombre }
        ).ToListAsync(ct);
        var porVenta = lineas.ToLookup(l => l.DocId);

        return new Pagina<DocumentoResumenDto>(filas.Select(x =>
        {
            var propias = porVenta[x.Doc.Id].ToList();
            var almacenes = string.Join(", ", propias.Select(l => l.Almacen).Distinct().Order());
            return Resumen(x.Doc, x.Tercero, almacenes, propias.Select(l => l.Importe));
        }).ToList(), total, f.NumeroPagina, f.Tamano);
    }

    public async Task<Pagina<DocumentoResumenDto>> ListarTraspasosAsync(FiltroDocumentos f, CancellationToken ct)
    {
        var q = from t in FiltrarDocumentos(db.Traspasos.AsNoTracking().IgnoreAutoIncludes(), f)
                join o in db.Almacenes on t.AlmacenOrigenId equals o.Id
                join d in db.Almacenes on t.AlmacenDestinoId equals d.Id
                select new { Doc = t, Origen = o.Nombre, Destino = d.Nombre, Lineas = t.Lineas.Count };
        if (BuscarTexto(f) is { } patron)
            q = q.Where(x => EF.Functions.ILike(x.Origen, patron) || EF.Functions.ILike(x.Destino, patron) || x.Doc.Folio.ToString() == f.Busqueda!.Trim());

        var total = await q.CountAsync(ct);
        var filas = await q.OrderByDescending(x => x.Doc.Folio).Skip(Saltar(f)).Take(f.Tamano).ToListAsync(ct);
        return new Pagina<DocumentoResumenDto>(filas.Select(x =>
                new DocumentoResumenDto(x.Doc.Id, x.Doc.Folio, x.Doc.Fecha, $"{x.Origen} → {x.Destino}", x.Destino, x.Doc.Estado, x.Lineas, 0, 0, 0))
            .ToList(), total, f.NumeroPagina, f.Tamano);
    }

    public async Task<DocumentoDetalleDto?> ObtenerRecepcionAsync(Guid id, CancellationToken ct)
    {
        var r = await db.Recepciones.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null)
            return null;
        var proveedor = await db.Proveedores.AsNoTracking().FirstAsync(p => p.Id == r.ProveedorId, ct);
        var almacen = await db.Almacenes.AsNoTracking().Where(a => a.Id == r.AlmacenId).Select(a => a.Nombre).FirstAsync(ct);
        var productos = await Productos(r.Lineas.Select(l => l.ProductoId), ct);
        var lineas = r.Lineas.Select(l => Linea(productos[l.ProductoId], null, l.Cantidad, l.CostoUnitario, l.TasaIva.Valor, l.Importe, l.CostoUnitario)).ToList();
        return await Detalle(r, proveedor.Nombre, proveedor.Rfc.Valor, proveedor.Correo.Valor, almacen, null, lineas, ct);
    }

    public async Task<DocumentoDetalleDto?> ObtenerVentaAsync(Guid id, CancellationToken ct)
    {
        var v = await db.Ventas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (v is null)
            return null;
        var cliente = await db.Clientes.AsNoTracking().FirstAsync(c => c.Id == v.ClienteId, ct);
        var productos = await Productos(v.Lineas.Select(l => l.ProductoId), ct);
        var almacenIds = v.Lineas.Select(l => l.AlmacenId).Distinct().ToList();
        var almacenes = await db.Almacenes.AsNoTracking().Where(a => almacenIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.Nombre, ct);
        var lineas = v.Lineas.Select(l => Linea(productos[l.ProductoId], almacenes[l.AlmacenId], l.Cantidad, l.PrecioUnitario, l.TasaIva.Valor, l.Importe, l.CostoUnitario)).ToList();
        return await Detalle(v, cliente.Nombre, cliente.Rfc.Valor, cliente.Correo.Valor, string.Join(", ", almacenes.Values.Order()), null, lineas, ct);
    }

    public async Task<DocumentoDetalleDto?> ObtenerTraspasoAsync(Guid id, CancellationToken ct)
    {
        var t = await db.Traspasos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null)
            return null;
        var nombres = await db.Almacenes.AsNoTracking().Where(a => a.Id == t.AlmacenOrigenId || a.Id == t.AlmacenDestinoId)
            .ToDictionaryAsync(a => a.Id, a => a.Nombre, ct);
        var productos = await Productos(t.Lineas.Select(l => l.ProductoId), ct);
        var lineas = t.Lineas.Select(l => Linea(productos[l.ProductoId], null, l.Cantidad, l.CostoUnitario, 0, Domain.ValueObjects.Importe.Cero, l.CostoUnitario)).ToList();
        return await Detalle(t, null, null, null, nombres[t.AlmacenOrigenId], nombres[t.AlmacenDestinoId], lineas, ct);
    }

    private static IQueryable<T> FiltrarDocumentos<T>(IQueryable<T> q, FiltroDocumentos f) where T : Documento
    {
        if (f.Estado is { } estado)
            q = q.Where(d => d.Estado == estado);
        if (f.Desde is { } desde)
            q = q.Where(d => EF.Property<DateOnly>(d, "Fecha") >= desde);
        if (f.Hasta is { } hasta)
            q = q.Where(d => EF.Property<DateOnly>(d, "Fecha") <= hasta);
        return q;
    }

    private static string? BuscarTexto(FiltroDocumentos f) =>
        string.IsNullOrWhiteSpace(f.Busqueda) ? null : $"%{f.Busqueda.Trim()}%";

    private static int Saltar(FiltroDocumentos f) => (Math.Max(f.NumeroPagina, 1) - 1) * f.Tamano;

    private static DocumentoResumenDto Resumen(Documento d, string tercero, string almacen, IEnumerable<Domain.ValueObjects.Importe> importes)
    {
        var lista = importes.ToList();
        var subtotal = lista.Sum(i => i.Subtotal.Monto);
        var iva = lista.Sum(i => i.Iva.Monto);
        var fecha = d switch { Recepcion r => r.Fecha, Venta v => v.Fecha, Traspaso t => t.Fecha, _ => default };
        return new DocumentoResumenDto(d.Id, d.Folio, fecha, tercero, almacen, d.Estado, lista.Count, subtotal, iva, subtotal + iva);
    }

    private sealed record ProductoBreve(string Referencia, string Descripcion, string Unidad);

    private async Task<Dictionary<Guid, ProductoBreve>> Productos(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var lista = ids.Distinct().ToList();
        return await (
            from p in db.Productos.AsNoTracking()
            join u in db.Unidades on p.UnidadId equals u.Id
            where lista.Contains(p.Id)
            select new { p.Id, Breve = new ProductoBreve(p.Referencia, p.Descripcion, u.Clave) }
        ).ToDictionaryAsync(x => x.Id, x => x.Breve, ct);
    }

    private static LineaDocumentoDto Linea(ProductoBreve p, string? almacen, decimal cantidad, decimal precio, decimal tasa,
        Domain.ValueObjects.Importe importe, decimal costo) =>
        new(Guid.Empty, p.Referencia, p.Descripcion, p.Unidad, almacen, cantidad, precio, tasa, importe.Subtotal.Monto, importe.Iva.Monto, costo);

    private async Task<DocumentoDetalleDto> Detalle(Documento d, string? tercero, string? rfc, string? correo, string almacen,
        string? destino, IReadOnlyList<LineaDocumentoDto> lineas, CancellationToken ct)
    {
        var nombres = await NombresUsuarios([d.Creado.UsuarioId, d.Cancelado?.UsuarioId ?? Guid.Empty], ct);
        var fecha = d switch { Recepcion r => r.Fecha, Venta v => v.Fecha, Traspaso t => t.Fecha, _ => default };
        return new DocumentoDetalleDto(d.Id, d.Folio, fecha, d.Estado, tercero, rfc, correo, almacen, destino, lineas,
            nombres.GetValueOrDefault(d.Creado.UsuarioId, "—"), d.Creado.Fecha,
            d.Cancelado is { } c ? nombres.GetValueOrDefault(c.UsuarioId, "—") : null, d.Cancelado?.Fecha, d.MotivoCancelacion);
    }

    // ---------------- Finanzas

    public async Task<ResumenFinancieroDto> ListarMovimientosFinancierosAsync(FiltroFinanzas f, CancellationToken ct)
    {
        var q = db.MovimientosFinancieros.AsNoTracking();
        if (f.Desde is { } desde)
        {
            var inicio = InicioDia(desde);
            q = q.Where(m => m.Registro.Fecha >= inicio);
        }
        if (f.Hasta is { } hasta)
        {
            var fin = InicioDia(hasta.AddDays(1));
            q = q.Where(m => m.Registro.Fecha < fin);
        }
        if (f.Naturaleza is { } naturaleza)
            q = q.Where(m => m.Naturaleza == naturaleza);

        var todos = await q.OrderByDescending(m => m.Registro.Fecha).ToListAsync(ct);
        var pagina = todos.Skip((Math.Max(f.NumeroPagina, 1) - 1) * f.Tamano).Take(f.Tamano).ToList();

        var folios = await Folios(pagina.Select(m => m.Documento), ct);
        var terceroIds = pagina.Select(m => m.TerceroId).Distinct().ToList();
        var terceros = (await db.Clientes.AsNoTracking().Where(c => terceroIds.Contains(c.Id)).Select(c => new { c.Id, c.Nombre }).ToListAsync(ct))
            .Concat(await db.Proveedores.AsNoTracking().Where(p => terceroIds.Contains(p.Id)).Select(p => new { p.Id, p.Nombre }).ToListAsync(ct))
            .ToDictionary(t => t.Id, t => t.Nombre);

        var ingresos = todos.Where(m => m.Naturaleza == NaturalezaFinanciera.Ingreso).ToList();
        var egresos = todos.Where(m => m.Naturaleza == NaturalezaFinanciera.Egreso).ToList();
        return new ResumenFinancieroDto(
            pagina.Select(m => new MovimientoFinancieroDto(m.Id, m.Registro.Fecha, m.Naturaleza, m.Documento.Tipo, m.Documento.Id,
                folios.GetValueOrDefault(m.Documento.Id), terceros.GetValueOrDefault(m.TerceroId, "—"),
                m.Importe.Subtotal.Monto, m.Importe.Iva.Monto, m.Importe.Total.Monto, m.EsReverso)).ToList(),
            todos.Count,
            ingresos.Sum(m => m.Importe.Total.Monto), egresos.Sum(m => m.Importe.Total.Monto),
            ingresos.Sum(m => m.Importe.Iva.Monto), egresos.Sum(m => m.Importe.Iva.Monto));
    }

    // ---------------- Reportes

    public async Task<ReporteVentasDto> ReporteVentasAsync(DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var ventas = await db.Ventas.AsNoTracking()
            .Where(v => v.Estado == EstadoDocumento.Confirmado && v.Fecha >= desde && v.Fecha <= hasta)
            .ToListAsync(ct);
        var lineas = ventas.SelectMany(v => v.Lineas.Select(l => (Venta: v, Linea: l))).ToList();

        var clienteIds = ventas.Select(v => v.ClienteId).Distinct().ToList();
        var clientes = await db.Clientes.AsNoTracking().Where(c => clienteIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Nombre, ct);
        var productos = await Productos(lineas.Select(x => x.Linea.ProductoId), ct);
        var almacenIds = lineas.Select(x => x.Linea.AlmacenId).Distinct().ToList();
        var almacenes = await db.Almacenes.AsNoTracking().Where(a => almacenIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.Nombre, ct);

        var subtotal = lineas.Sum(x => x.Linea.Importe.Subtotal.Monto);
        var iva = lineas.Sum(x => x.Linea.Importe.Iva.Monto);
        return new ReporteVentasDto(ventas.Count, subtotal, iva, subtotal + iva,
            PorPeriodo(desde, hasta, lineas.Select(x => (x.Venta.Fecha, x.Linea.Importe.Total.Monto))),
            Top(lineas.GroupBy(x => clientes[x.Venta.ClienteId]), g => g.Sum(x => x.Linea.Importe.Total.Monto)),
            Top(lineas.GroupBy(x => productos[x.Linea.ProductoId].Referencia), g => g.Sum(x => x.Linea.Importe.Subtotal.Monto)),
            Top(lineas.GroupBy(x => almacenes[x.Linea.AlmacenId]), g => g.Sum(x => x.Linea.Importe.Subtotal.Monto)));
    }

    public async Task<ReporteComprasDto> ReporteComprasAsync(DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var recepciones = await db.Recepciones.AsNoTracking()
            .Where(r => r.Estado == EstadoDocumento.Confirmado && r.Fecha >= desde && r.Fecha <= hasta)
            .ToListAsync(ct);
        var proveedorIds = recepciones.Select(r => r.ProveedorId).Distinct().ToList();
        var proveedores = await db.Proveedores.AsNoTracking().Where(p => proveedorIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Nombre, ct);
        var lineas = recepciones.SelectMany(r => r.Lineas.Select(l => (Recepcion: r, Linea: l))).ToList();

        var subtotal = lineas.Sum(x => x.Linea.Importe.Subtotal.Monto);
        var iva = lineas.Sum(x => x.Linea.Importe.Iva.Monto);
        return new ReporteComprasDto(recepciones.Count, subtotal, iva, subtotal + iva,
            PorPeriodo(desde, hasta, lineas.Select(x => (x.Recepcion.Fecha, x.Linea.Importe.Total.Monto))),
            Top(lineas.GroupBy(x => proveedores[x.Recepcion.ProveedorId]), g => g.Sum(x => x.Linea.Importe.Total.Monto)));
    }

    public async Task<ReporteFinancieroDto> ReporteFinancieroAsync(DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var inicio = InicioDia(desde);
        var fin = InicioDia(hasta.AddDays(1));
        var movimientos = await db.MovimientosFinancieros.AsNoTracking()
            .Where(m => m.Registro.Fecha >= inicio && m.Registro.Fecha < fin)
            .ToListAsync(ct);

        var ingresos = PorPeriodo(desde, hasta, movimientos.Where(m => m.Naturaleza == NaturalezaFinanciera.Ingreso)
            .Select(m => (FechaLocal(m.Registro.Fecha), m.Importe.Total.Monto)));
        var egresos = PorPeriodo(desde, hasta, movimientos.Where(m => m.Naturaleza == NaturalezaFinanciera.Egreso)
            .Select(m => (FechaLocal(m.Registro.Fecha), m.Importe.Total.Monto)));

        return new ReporteFinancieroDto(
            ingresos.Select(p => p.Etiqueta).ToList(), ingresos.Select(p => p.Valor).ToList(), egresos.Select(p => p.Valor).ToList(),
            ingresos.Sum(p => p.Valor), egresos.Sum(p => p.Valor),
            movimientos.Where(m => m.Naturaleza == NaturalezaFinanciera.Ingreso).Sum(m => m.Importe.Iva.Monto),
            movimientos.Where(m => m.Naturaleza == NaturalezaFinanciera.Egreso).Sum(m => m.Importe.Iva.Monto));
    }

    public async Task<ReporteInventarioDto> ReporteInventarioAsync(CancellationToken ct)
    {
        var filas = (await ConsultaExistencias().Where(x => x.A.Activo).ToListAsync(ct)).Select(ADto).ToList();
        return new ReporteInventarioDto(
            filas.Sum(e => e.Valor),
            filas.GroupBy(e => e.Almacen).Select(g => new PuntoDto(g.Key, g.Sum(e => e.Valor))).OrderByDescending(p => p.Valor).ToList(),
            filas.Where(e => e.BajoMinimo).OrderBy(e => e.Almacen).ThenBy(e => e.Referencia).ToList());
    }

    public async Task<IReadOnlyList<MargenProductoDto>> ReporteMargenAsync(DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var lineas = (await db.Ventas.AsNoTracking()
                .Where(v => v.Estado == EstadoDocumento.Confirmado && v.Fecha >= desde && v.Fecha <= hasta)
                .ToListAsync(ct))
            .SelectMany(v => v.Lineas).ToList();
        var productos = await Productos(lineas.Select(l => l.ProductoId), ct);
        return lineas.GroupBy(l => l.ProductoId)
            .Select(g => new MargenProductoDto(productos[g.Key].Referencia, productos[g.Key].Descripcion,
                g.Sum(l => l.Cantidad), g.Sum(l => l.Importe.Subtotal.Monto), Redondear(g.Sum(l => l.Cantidad * l.CostoUnitario))))
            .OrderByDescending(m => m.Margen).ToList();
    }

    // ---------------- Apoyo

    private async Task<Dictionary<Guid, string>> NombresUsuarios(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var lista = ids.Where(i => i != Guid.Empty).Distinct().ToList();
        return await db.Users.AsNoTracking().Where(u => lista.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Nombre, ct);
    }

    private async Task<Dictionary<Guid, long>> Folios(IEnumerable<DocumentoRef> documentos, CancellationToken ct)
    {
        var porTipo = documentos.GroupBy(d => d.Tipo).ToDictionary(g => g.Key, g => g.Select(d => d.Id).Distinct().ToList());
        var folios = new Dictionary<Guid, long>();
        if (porTipo.TryGetValue(TipoDocumento.Recepcion, out var r))
            foreach (var x in await db.Recepciones.AsNoTracking().IgnoreAutoIncludes().Where(d => r.Contains(d.Id)).Select(d => new { d.Id, d.Folio }).ToListAsync(ct))
                folios[x.Id] = x.Folio;
        if (porTipo.TryGetValue(TipoDocumento.Venta, out var v))
            foreach (var x in await db.Ventas.AsNoTracking().IgnoreAutoIncludes().Where(d => v.Contains(d.Id)).Select(d => new { d.Id, d.Folio }).ToListAsync(ct))
                folios[x.Id] = x.Folio;
        if (porTipo.TryGetValue(TipoDocumento.Traspaso, out var t))
            foreach (var x in await db.Traspasos.AsNoTracking().IgnoreAutoIncludes().Where(d => t.Contains(d.Id)).Select(d => new { d.Id, d.Folio }).ToListAsync(ct))
                folios[x.Id] = x.Folio;
        return folios;
    }

    /// <summary>Por día si el periodo es de hasta 62 días; si no, por mes. Incluye los periodos sin movimiento.</summary>
    private static IReadOnlyList<PuntoDto> PorPeriodo(DateOnly desde, DateOnly hasta, IEnumerable<(DateOnly Fecha, decimal Monto)> datos)
    {
        var porDia = hasta.DayNumber - desde.DayNumber <= 62;
        Func<DateOnly, DateOnly> clave = porDia ? d => d : d => new DateOnly(d.Year, d.Month, 1);
        var sumas = datos.GroupBy(x => clave(x.Fecha)).ToDictionary(g => g.Key, g => g.Sum(x => x.Monto));

        var puntos = new List<PuntoDto>();
        for (var actual = clave(desde); actual <= hasta; actual = porDia ? actual.AddDays(1) : actual.AddMonths(1))
            puntos.Add(new PuntoDto(actual.ToString(porDia ? "dd MMM" : "MMM yyyy", Mx), sumas.GetValueOrDefault(actual)));
        return puntos;
    }

    private static IReadOnlyList<PuntoDto> Top<T>(IEnumerable<IGrouping<string, T>> grupos, Func<IGrouping<string, T>, decimal> valor) =>
        grupos.Select(g => new PuntoDto(g.Key, valor(g))).OrderByDescending(p => p.Valor).Take(10).ToList();

    private static DateTimeOffset InicioDia(DateOnly fecha)
    {
        var local = fecha.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, ZonaMexico.GetUtcOffset(local)).ToUniversalTime();
    }

    private static DateOnly FechaLocal(DateTimeOffset fecha) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(fecha, ZonaMexico).DateTime);

    private static decimal Redondear(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);
}
