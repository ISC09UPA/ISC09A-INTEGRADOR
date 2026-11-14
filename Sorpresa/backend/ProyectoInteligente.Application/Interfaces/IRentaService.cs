using LockerSmart.Domain.Entities;

namespace LockerSmart.Application.Interfaces;

public interface IRentaService
{
    Task<Renta?> CrearAsync(
        int usuarioId,
        double pesoDeclaradoKg,
        int duracionHoras);

    Task<Renta?> ObtenerAsync(int id);

    Task<List<Renta>> ListarPorUsuarioAsync(int usuarioId);

    Task<List<Renta>> ListarActivasAsync();

    Task<bool> FinalizarAsync(
        int id,
        double? pesoFinalKg);

    Task<int> ActualizarVencidasAsync();
}