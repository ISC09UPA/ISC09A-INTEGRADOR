using AlmacenInteligente.Domain.Common;

namespace AlmacenInteligente.Domain.Inventario;

/// <summary>
/// Las existencias que toca una operación, cargadas por la capa de aplicación. Las que no existían
/// y se crean en una entrada quedan en <see cref="Nuevas"/> para que se agreguen al repositorio.
/// </summary>
public sealed class LoteExistencias
{
    private readonly Dictionary<(Guid AlmacenId, Guid ProductoId), Existencia> _existencias;
    private readonly List<Existencia> _nuevas = [];

    public LoteExistencias(IEnumerable<Existencia> existencias) =>
        _existencias = existencias.ToDictionary(e => (e.AlmacenId, e.ProductoId));

    public IReadOnlyCollection<Existencia> Todas => _existencias.Values;

    public IReadOnlyList<Existencia> Nuevas => _nuevas;

    public Existencia ParaSalida(Guid almacenId, Guid productoId) =>
        _existencias.TryGetValue((almacenId, productoId), out var existencia)
            ? existencia
            : throw new DomainException("El producto no tiene existencia en ese almacén.");

    public Existencia ParaEntrada(Guid almacenId, Guid productoId)
    {
        if (_existencias.TryGetValue((almacenId, productoId), out var existencia))
            return existencia;

        existencia = new Existencia(almacenId, productoId);
        _existencias.Add((almacenId, productoId), existencia);
        _nuevas.Add(existencia);
        return existencia;
    }
}
