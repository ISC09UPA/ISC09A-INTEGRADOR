using CiudadInteligente.Domain.Entities;

namespace CiudadInteligente.Application.Interfaces;

/// <summary>Historial de conteos de la camara (EF Core + SQLite en Infrastructure).</summary>
public interface IConteoTraficoRepository
{
    Task GuardarAsync(ConteoTrafico conteo);

    /// <summary>Varios de una vez, en una sola transaccion (carga de historial).</summary>
    Task GuardarVariosAsync(IReadOnlyList<ConteoTrafico> conteos);

    Task<IReadOnlyList<ConteoTrafico>> ObtenerRecientesAsync(int cantidad);

    /// <summary>Todos los conteos desde esa fecha, del mas viejo al mas nuevo (para las estadisticas).</summary>
    Task<IReadOnlyList<ConteoTrafico>> ObtenerDesdeAsync(DateTimeOffset desde);
}
