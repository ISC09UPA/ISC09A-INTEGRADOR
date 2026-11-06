using LockerSmart.Domain.Entities;
using LockerSmart.Domain.Enums;

namespace LockerSmart.Application.Interfaces;

public interface ILockerService
{
    Task<List<Locker>> ListarAsync(bool soloDisponibles);
    Task<Locker?> ObtenerAsync(int id);
    Task<Locker> CrearAsync(Locker locker);
    Task<bool> CambiarEstadoAsync(int id, LockerEstado estado);
}
