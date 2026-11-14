using AlmacenInteligente.Application.Movimientos;
using AlmacenInteligente.Application.Terceros;
using AlmacenInteligente.Domain.Inventario;
using Microsoft.EntityFrameworkCore;

namespace AlmacenInteligente.Infrastructure.Persistencia;

/// <summary>Reporte de movimientos sobre el kardex: lista paginada, totales y resumen por producto y almacén.</summary>
internal sealed class ConsultasMovimientosEf(AlmacenDbContext db) : IConsultasMovimientos
{
    /// <summary>Tope de filas para exportar; un periodo más grande se debe acotar con filtros.</summary>
    private const int MaximoExportacion = 20_000;

    private static readonly TimeZoneInfo ZonaMexico = TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");

    private sealed class Fila
    {
        public MovimientoInventario M { get; init; } = null!;
        public string Almacen { get; init; } = string.Empty;
        public string Referencia { get; init; } = string.Empty;
        public string Descripcion { get; init; } = string.Empty;
        public string Unidad { get; init; } = string.Empty;
    }

    public async Task<ReporteMovimientosDto> ReporteAsync(FiltroMovimientos f, bool todos, CancellationToken ct)
    {
        var inicio = InicioDia(f.Desde);
        var fin = InicioDia(f.Hasta.AddDays(1));

        var q = from m in db.Kardex.AsNoTracking()
                join a in db.Almacenes on m.AlmacenId equals a.Id
                join p in db.Productos on m.ProductoId equals p.Id
                join u in db.Unidades on p.UnidadId equals u.Id
                where m.Registro.Fecha >= inicio && m.Registro.Fecha < fin
                select new Fila { M = m, Almacen = a.Nombre, Referencia = p.Referencia, Descripcion = p.Descripcion, Unidad = u.Clave };
        if (f.AlmacenId is { } almacenId)
            q = q.Where(x => x.M.AlmacenId == almacenId);
        if (f.ProductoId is { } productoId)
            q = q.Where(x => x.M.ProductoId == productoId);
        if (f.Tipo is { } tipo)
            q = q.Where(x => x.M.Tipo == tipo);
        if (!string.IsNullOrWhiteSpace(f.Busqueda))
        {
            var patron = $"%{f.Busqueda.Trim()}%";
            q = q.Where(x => EF.Functions.ILike(x.Referencia, patron) || EF.Functions.ILike(x.Descripcion, patron));
        }

        // Totales y resumen sobre todo el filtro; la lista, paginada (o completa para exportar).
        var totales = await q.GroupBy(_ => 1).Select(g => new
        {
            Numero = g.Count(),
            Entradas = g.Sum(x => x.M.Cantidad > 0 ? x.M.Cantidad : 0),
            Salidas = g.Sum(x => x.M.Cantidad < 0 ? -x.M.Cantidad : 0),
            ValorEntradas = g.Sum(x => x.M.Cantidad > 0 ? x.M.Cantidad * x.M.CostoUnitario : 0),
            ValorSalidas = g.Sum(x => x.M.Cantidad < 0 ? -x.M.Cantidad * x.M.CostoUnitario : 0),
        }).FirstOrDefaultAsync(ct);

        var ordenada = q.OrderByDescending(x => x.M.Registro.Fecha).ThenByDescending(x => x.M.Id);
        var filas = todos
            ? await ordenada.Take(MaximoExportacion).ToListAsync(ct)
            : await ordenada.Skip((Math.Max(f.NumeroPagina, 1) - 1) * f.Tamano).Take(Math.Clamp(f.Tamano, 1, 500)).ToListAsync(ct);

        var porProducto = await Resumen(q, inicio, ct);
        var folios = await Folios(filas.Where(x => x.M.Documento is not null).Select(x => x.M.Documento!), ct);
        var usuarioIds = filas.Select(x => x.M.Registro.UsuarioId).Distinct().ToList();
        var usuarios = await db.Users.AsNoTracking().Where(u => usuarioIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Nombre, ct);

        var movimientos = filas.Select(x => new MovimientoDto(
            x.M.Id, x.M.Registro.Fecha, x.Almacen, x.M.ProductoId, x.Referencia, x.Descripcion, x.Unidad, x.M.Tipo,
            x.M.Cantidad, x.M.CostoUnitario, x.M.SaldoCantidad, x.M.Documento?.Tipo, x.M.Documento?.Id,
            x.M.Documento is { } d ? folios.GetValueOrDefault(d.Id) : null,
            usuarios.GetValueOrDefault(x.M.Registro.UsuarioId, "—"))).ToList();

        return new ReporteMovimientosDto(
            new Pagina<MovimientoDto>(movimientos, totales?.Numero ?? 0, todos ? 1 : f.NumeroPagina, todos ? movimientos.Count : f.Tamano),
            totales?.Numero ?? 0,
            totales?.Entradas ?? 0, totales?.Salidas ?? 0,
            Redondear(totales?.ValorEntradas ?? 0), Redondear(totales?.ValorSalidas ?? 0),
            porProducto);
    }

    /// <summary>
    /// Por cada producto y almacén con movimientos en el periodo: saldo antes del periodo (último movimiento previo),
    /// entradas, salidas y saldo final (inicial + entradas − salidas).
    /// </summary>
    private async Task<IReadOnlyList<ResumenProductoDto>> Resumen(IQueryable<Fila> q, DateTimeOffset inicio, CancellationToken ct)
    {
        var grupos = await q
            .GroupBy(x => new { x.M.AlmacenId, x.M.ProductoId, x.Almacen, x.Referencia, x.Descripcion, x.Unidad })
            .Select(g => new
            {
                g.Key.AlmacenId, g.Key.ProductoId, g.Key.Almacen, g.Key.Referencia, g.Key.Descripcion, g.Key.Unidad,
                Entradas = g.Sum(x => x.M.Cantidad > 0 ? x.M.Cantidad : 0),
                Salidas = g.Sum(x => x.M.Cantidad < 0 ? -x.M.Cantidad : 0),
                ValorEntradas = g.Sum(x => x.M.Cantidad > 0 ? x.M.Cantidad * x.M.CostoUnitario : 0),
                ValorSalidas = g.Sum(x => x.M.Cantidad < 0 ? -x.M.Cantidad * x.M.CostoUnitario : 0),
            })
            .ToListAsync(ct);

        var almacenes = grupos.Select(g => g.AlmacenId).Distinct().ToList();
        var productos = grupos.Select(g => g.ProductoId).Distinct().ToList();
        var anteriores = await db.Kardex.AsNoTracking()
            .Where(m => m.Registro.Fecha < inicio && almacenes.Contains(m.AlmacenId) && productos.Contains(m.ProductoId))
            .GroupBy(m => new { m.AlmacenId, m.ProductoId })
            .Select(g => new
            {
                g.Key.AlmacenId, g.Key.ProductoId,
                Saldo = g.OrderByDescending(m => m.Registro.Fecha).ThenByDescending(m => m.Id).Select(m => m.SaldoCantidad).First(),
            })
            .ToListAsync(ct);
        var saldoInicial = anteriores.ToDictionary(a => (a.AlmacenId, a.ProductoId), a => a.Saldo);

        return grupos
            .Select(g =>
            {
                var inicial = saldoInicial.GetValueOrDefault((g.AlmacenId, g.ProductoId));
                return new ResumenProductoDto(g.Almacen, g.Referencia, g.Descripcion, g.Unidad,
                    inicial, g.Entradas, g.Salidas, inicial + g.Entradas - g.Salidas, Redondear(g.ValorEntradas), Redondear(g.ValorSalidas));
            })
            .OrderBy(r => r.Almacen).ThenBy(r => r.Referencia)
            .ToList();
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

    private static DateTimeOffset InicioDia(DateOnly fecha)
    {
        var local = fecha.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, ZonaMexico.GetUtcOffset(local)).ToUniversalTime();
    }

    private static decimal Redondear(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
