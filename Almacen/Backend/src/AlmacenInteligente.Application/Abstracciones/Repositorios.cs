using AlmacenInteligente.Domain.Almacenes;
using AlmacenInteligente.Domain.Common;
using AlmacenInteligente.Domain.Inventario;
using AlmacenInteligente.Domain.Productos;
using AlmacenInteligente.Domain.Recepciones;
using AlmacenInteligente.Domain.Terceros;
using AlmacenInteligente.Domain.Ventas;

namespace AlmacenInteligente.Application.Abstracciones;

public interface IRepositorio<T> where T : AggregateRoot
{
    Task<T?> ObtenerAsync(Guid id, CancellationToken ct = default);
    void Agregar(T entidad);
}

public interface ITerceroRepositorio<T> : IRepositorio<T> where T : Tercero
{
    Task<bool> ExisteCodigoAsync(string codigo, CancellationToken ct = default);

    /// <summary>Busca por código, nombre, RFC o correo; ordena por nombre. Las páginas empiezan en 1.</summary>
    Task<(IReadOnlyList<T> Elementos, int Total)> BuscarAsync(
        string? texto, bool incluirInactivos, int numeroPagina, int tamano, CancellationToken ct = default);
}

public interface IClienteRepositorio : ITerceroRepositorio<Cliente>;

public interface IProveedorRepositorio : ITerceroRepositorio<Proveedor>;

public interface IUnidadRepositorio
{
    Task<Unidad?> ObtenerAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExisteClaveAsync(string clave, CancellationToken ct = default);
    void Agregar(Unidad unidad);
}

public interface IProductoRepositorio : IRepositorio<Producto>
{
    Task<bool> ExisteReferenciaAsync(string referencia, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, Producto>> ObtenerVariosAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
}

public interface IAlmacenRepositorio : IRepositorio<Almacen>
{
    Task<bool> ExisteNombreAsync(string nombre, Guid? excepto, CancellationToken ct = default);
}

public interface IRecepcionRepositorio : IRepositorio<Recepcion>;

public interface IVentaRepositorio : IRepositorio<Venta>;

public interface ITraspasoRepositorio : IRepositorio<Traspaso>;

public interface IExistenciaRepositorio
{
    /// <summary>Carga las existencias de esos pares (almacén, producto) con control de concurrencia.</summary>
    Task<LoteExistencias> CargarLoteAsync(IEnumerable<(Guid AlmacenId, Guid ProductoId)> claves, CancellationToken ct = default);

    /// <summary>Agrega las existencias nuevas del lote y el kardex/financiero que produjo la operación.</summary>
    void Registrar(LoteExistencias lote, ResultadoInventario resultado);
}
