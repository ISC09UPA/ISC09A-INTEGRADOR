using CiudadInteligente.Domain.Entities;

namespace CiudadInteligente.Application.Interfaces;

/// <summary>Historial de conteos de la camara (Dapper + SQLite en Infrastructure).</summary>
public interface IConteoTraficoRepository
{
    Task GuardarAsync(ConteoTrafico conteo);

    Task<IReadOnlyList<ConteoTrafico>> ObtenerRecientesAsync(int cantidad);
}
