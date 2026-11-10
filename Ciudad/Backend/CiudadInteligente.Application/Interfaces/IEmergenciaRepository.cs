using CiudadInteligente.Domain.Entities;

namespace CiudadInteligente.Application.Interfaces;

/// <summary>Historial de corredores de emergencia (EF Core + SQLite en Infrastructure).</summary>
public interface IEmergenciaRepository
{
    /// <returns>El Id que genero la base de datos.</returns>
    Task<int> RegistrarAsync(Emergencia emergencia);

    /// <summary>Marca como finalizadas todas las emergencias que sigan activas.</summary>
    Task<int> FinalizarActivasAsync(DateTimeOffset finalizadaEn);

    Task<IReadOnlyList<Emergencia>> ObtenerRecientesAsync(int cantidad);
}
