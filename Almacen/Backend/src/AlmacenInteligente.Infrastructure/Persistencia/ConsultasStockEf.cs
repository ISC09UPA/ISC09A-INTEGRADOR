using AlmacenInteligente.Application.Iot;
using AlmacenInteligente.Application.Terceros;
using AlmacenInteligente.Domain.Inventario;
using AlmacenInteligente.Domain.Iot;
using AlmacenInteligente.Domain.Productos;
using Microsoft.EntityFrameworkCore;

namespace AlmacenInteligente.Infrastructure.Persistencia;

/// <summary>
/// Semáforo de stock: productos aprobados y activos con existencia y mínimo sumados en los almacenes activos.
/// El nivel se calcula en la base con las mismas reglas de <see cref="SemaforoStock"/> para poder filtrar y contar.
/// </summary>
internal sealed class ConsultasStockEf(AlmacenDbContext db) : IConsultasStock
{
    private static readonly TimeZoneInfo ZonaMexico = TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");

    private sealed class FilaSemaforo
    {
        public Guid Id { get; init; }
        public string Referencia { get; init; } = null!;
        public string Descripcion { get; init; } = null!;
        public string Unidad { get; init; } = null!;
        public string? CodigoNfc { get; init; }
        public decimal Existencia { get; init; }
        public decimal Minimo { get; init; }
    }

    private IQueryable<FilaSemaforo> Productos(string? busqueda, Guid? almacenId)
    {
        var existencias = from e in db.Existencias
                          join a in db.Almacenes on e.AlmacenId equals a.Id
                          where a.Activo && (almacenId == null || e.AlmacenId == almacenId)
                          select e;
        var q = from p in db.Productos.AsNoTracking()
                join u in db.Unidades on p.UnidadId equals u.Id
                where p.Activo && p.Estado == EstadoAprobacion.Aprobado
                select new FilaSemaforo
                {
                    Id = p.Id,
                    Referencia = p.Referencia,
                    Descripcion = p.Descripcion,
                    Unidad = u.Clave,
                    CodigoNfc = p.CodigoNfc,
                    Existencia = existencias.Where(e => e.ProductoId == p.Id).Sum(e => (decimal?)e.Cantidad) ?? 0,
                    Minimo = existencias.Where(e => e.ProductoId == p.Id).Sum(e => (decimal?)e.StockMinimo) ?? 0,
                };
        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var patron = $"%{busqueda.Trim()}%";
            var nfc = busqueda.Replace(":", "").Replace("-", "").Replace(" ", "").ToUpperInvariant();
            q = q.Where(x => EF.Functions.ILike(x.Referencia, patron) || EF.Functions.ILike(x.Descripcion, patron) || x.CodigoNfc == nfc);
        }
        return q;
    }

    private static IQueryable<FilaSemaforo> DeNivel(IQueryable<FilaSemaforo> q, NivelStock nivel) => nivel switch
    {
        NivelStock.Rojo => q.Where(x => x.Existencia <= 0),
        NivelStock.Amarillo => q.Where(x => x.Existencia > 0 && x.Minimo > 0 && x.Existencia <= x.Minimo),
        _ => q.Where(x => x.Existencia > 0 && (x.Minimo <= 0 || x.Existencia > x.Minimo)),
    };

    public async Task<SemaforoStockDto> SemaforoAsync(FiltroSemaforoStock f, CancellationToken ct = default)
    {
        var todos = Productos(f.Busqueda, f.AlmacenId);
        var verdes = await DeNivel(todos, NivelStock.Verde).CountAsync(ct);
        var amarillos = await DeNivel(todos, NivelStock.Amarillo).CountAsync(ct);
        var rojos = await DeNivel(todos, NivelStock.Rojo).CountAsync(ct);

        var q = f.Nivel is { } nivel ? DeNivel(todos, nivel) : todos;
        var total = f.Nivel switch { NivelStock.Verde => verdes, NivelStock.Amarillo => amarillos, NivelStock.Rojo => rojos, _ => verdes + amarillos + rojos };
        // Primero lo urgente: rojo, luego amarillo, luego verde.
        var filas = await q
            .OrderBy(x => x.Existencia <= 0 ? 0 : x.Minimo > 0 && x.Existencia <= x.Minimo ? 1 : 2)
            .ThenBy(x => x.Referencia)
            .Skip((f.NumeroPagina - 1) * f.Tamano).Take(f.Tamano)
            .ToListAsync(ct);

        var ids = filas.Select(x => x.Id).ToList();
        var porAlmacen = (await (
                from e in db.Existencias.AsNoTracking()
                join a in db.Almacenes on e.AlmacenId equals a.Id
                where a.Activo && ids.Contains(e.ProductoId) && (f.AlmacenId == null || e.AlmacenId == f.AlmacenId)
                orderby a.Nombre
                select new { e.ProductoId, a.Id, a.Nombre, e.Cantidad, e.StockMinimo })
            .ToListAsync(ct))
            .ToLookup(x => x.ProductoId);

        var elementos = filas.Select(x => new SemaforoProductoDto(
                x.Id, x.Referencia, x.Descripcion, x.Unidad, x.CodigoNfc, x.Existencia, x.Minimo, SemaforoStock.Nivel(x.Existencia, x.Minimo),
                porAlmacen[x.Id].Select(a => new SemaforoAlmacenDto(a.Id, a.Nombre, a.Cantidad, a.StockMinimo, SemaforoStock.Nivel(a.Cantidad, a.StockMinimo))).ToList()))
            .ToList();
        return new SemaforoStockDto(new Pagina<SemaforoProductoDto>(elementos, total, f.NumeroPagina, f.Tamano), verdes, amarillos, rojos);
    }

    public async Task<(decimal Existencia, decimal Minimo)> TotalesAsync(Guid productoId, Guid? almacenId, CancellationToken ct = default)
    {
        var fila = await Productos(null, almacenId).Where(x => x.Id == productoId).Select(x => new { x.Existencia, x.Minimo }).FirstOrDefaultAsync(ct);
        if (fila is not null)
            return (fila.Existencia, fila.Minimo);
        // Producto no aprobado o inactivo: igual se reporta lo que hay.
        var existencias = db.Existencias.AsNoTracking().Where(e => e.ProductoId == productoId && (almacenId == null || e.AlmacenId == almacenId));
        return (await existencias.SumAsync(e => (decimal?)e.Cantidad, ct) ?? 0, await existencias.SumAsync(e => (decimal?)e.StockMinimo, ct) ?? 0);
    }

    public async Task<Pagina<ConsultaStockIotDto>> ListarConsultasAsync(FiltroConsultasStockIot f, CancellationToken ct = default)
    {
        var q = db.ConsultasStockIot.AsNoTracking();
        if (f.AlmacenId is { } almacenId)
            q = q.Where(c => c.AlmacenId == almacenId);
        var total = await q.CountAsync(ct);
        var filas = await (
            from c in q.OrderByDescending(c => c.Fecha).Skip((f.NumeroPagina - 1) * f.Tamano).Take(f.Tamano)
            join p in db.Productos on c.ProductoId equals p.Id into productos
            from p in productos.DefaultIfEmpty()
            select new { c, Producto = p == null ? null : p.Referencia + " · " + p.Descripcion }
        ).ToListAsync(ct);
        var elementos = filas
            .Select(x => new ConsultaStockIotDto(x.c.Id, TimeZoneInfo.ConvertTime(x.c.Fecha, ZonaMexico), x.c.Uid, x.c.Familia, x.Producto,
                x.c.Existencia, x.c.Minimo, x.c.Nivel, x.c.Motivo))
            .ToList();
        return new Pagina<ConsultaStockIotDto>(elementos, total, f.NumeroPagina, f.Tamano);
    }

    public void AgregarConsulta(ConsultaStockIot consulta) => db.ConsultasStockIot.Add(consulta);
}
