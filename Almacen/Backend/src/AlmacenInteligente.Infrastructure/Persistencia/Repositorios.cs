using AlmacenInteligente.Application.Abstracciones;
using AlmacenInteligente.Domain.Almacenes;
using AlmacenInteligente.Domain.Common;
using AlmacenInteligente.Domain.Inventario;
using AlmacenInteligente.Domain.Productos;
using AlmacenInteligente.Domain.Recepciones;
using AlmacenInteligente.Domain.Terceros;
using AlmacenInteligente.Domain.Ventas;
using Microsoft.EntityFrameworkCore;

namespace AlmacenInteligente.Infrastructure.Persistencia;

internal abstract class Repositorio<T>(AlmacenDbContext db) : IRepositorio<T> where T : AggregateRoot
{
    protected AlmacenDbContext Db { get; } = db;

    public virtual Task<T?> ObtenerAsync(Guid id, CancellationToken ct = default) =>
        Db.Set<T>().FirstOrDefaultAsync(e => e.Id == id, ct);

    public void Agregar(T entidad) => Db.Set<T>().Add(entidad);
}

internal abstract class TerceroRepositorio<T>(AlmacenDbContext db) : Repositorio<T>(db), ITerceroRepositorio<T>
    where T : Tercero
{
    public Task<bool> ExisteCodigoAsync(string codigo, CancellationToken ct = default) =>
        Db.Set<T>().AnyAsync(t => t.Codigo == codigo, ct);

    public async Task<(IReadOnlyList<T> Elementos, int Total)> BuscarAsync(
        string? texto, bool incluirInactivos, int numeroPagina, int tamano, CancellationToken ct = default)
    {
        var consulta = Db.Set<T>().AsNoTracking();
        if (!incluirInactivos)
            consulta = consulta.Where(t => t.Activo);
        if (!string.IsNullOrWhiteSpace(texto))
        {
            var patron = $"%{texto.Trim()}%";
            consulta = consulta.Where(t =>
                EF.Functions.ILike(t.Codigo, patron) ||
                EF.Functions.ILike(t.Nombre, patron) ||
                EF.Functions.ILike((string)(object)t.Rfc, patron) ||
                EF.Functions.ILike((string)(object)t.Correo, patron));
        }

        var total = await consulta.CountAsync(ct);
        var elementos = await consulta
            .OrderBy(t => t.Nombre)
            .Skip((Math.Max(numeroPagina, 1) - 1) * tamano)
            .Take(Math.Clamp(tamano, 1, 200))
            .ToListAsync(ct);
        return (elementos, total);
    }
}

internal sealed class ClienteRepositorio(AlmacenDbContext db) : TerceroRepositorio<Cliente>(db), IClienteRepositorio;

internal sealed class ProveedorRepositorio(AlmacenDbContext db) : TerceroRepositorio<Proveedor>(db), IProveedorRepositorio;

internal sealed class UnidadRepositorio(AlmacenDbContext db) : IUnidadRepositorio
{
    public Task<Unidad?> ObtenerAsync(Guid id, CancellationToken ct = default) => db.Unidades.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<bool> ExisteClaveAsync(string clave, CancellationToken ct = default) => db.Unidades.AnyAsync(u => u.Clave == clave, ct);

    public void Agregar(Unidad unidad) => db.Unidades.Add(unidad);
}

internal sealed class ProductoRepositorio(AlmacenDbContext db) : Repositorio<Producto>(db), IProductoRepositorio
{
    public Task<bool> ExisteReferenciaAsync(string referencia, CancellationToken ct = default) =>
        Db.Productos.AnyAsync(p => p.Referencia == referencia, ct);

    public async Task<IReadOnlyDictionary<Guid, Producto>> ObtenerVariosAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var lista = ids.Distinct().ToList();
        return await Db.Productos.Where(p => lista.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
    }
}

internal sealed class AlmacenRepositorio(AlmacenDbContext db) : Repositorio<Almacen>(db), IAlmacenRepositorio
{
    public Task<bool> ExisteNombreAsync(string nombre, Guid? excepto, CancellationToken ct = default) =>
        Db.Almacenes.AnyAsync(a => a.Nombre.ToLower() == nombre.ToLower() && a.Id != excepto, ct);
}

internal sealed class RecepcionRepositorio(AlmacenDbContext db) : Repositorio<Recepcion>(db), IRecepcionRepositorio;

internal sealed class VentaRepositorio(AlmacenDbContext db) : Repositorio<Venta>(db), IVentaRepositorio;

internal sealed class TraspasoRepositorio(AlmacenDbContext db) : Repositorio<Traspaso>(db), ITraspasoRepositorio;

internal sealed class ExistenciaRepositorio(AlmacenDbContext db) : IExistenciaRepositorio
{
    public async Task<LoteExistencias> CargarLoteAsync(IEnumerable<(Guid AlmacenId, Guid ProductoId)> claves, CancellationToken ct = default)
    {
        var pares = claves.Distinct().ToList();
        var almacenes = pares.Select(p => p.AlmacenId).Distinct().ToList();
        var productos = pares.Select(p => p.ProductoId).Distinct().ToList();

        // Trae el producto cruzado y filtra en memoria: son pocas filas por documento.
        var candidatas = await db.Existencias
            .Where(e => almacenes.Contains(e.AlmacenId) && productos.Contains(e.ProductoId))
            .ToListAsync(ct);
        return new LoteExistencias(candidatas.Where(e => pares.Contains((e.AlmacenId, e.ProductoId))));
    }

    public void Registrar(LoteExistencias lote, ResultadoInventario resultado)
    {
        db.Existencias.AddRange(lote.Nuevas);
        db.Kardex.AddRange(resultado.Kardex);
        if (resultado.Financiero is not null)
            db.MovimientosFinancieros.Add(resultado.Financiero);
    }
}
