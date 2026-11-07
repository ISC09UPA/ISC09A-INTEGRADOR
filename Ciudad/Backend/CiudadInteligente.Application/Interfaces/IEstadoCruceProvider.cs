using CiudadInteligente.Domain.Models;

namespace CiudadInteligente.Application.Interfaces;

/// <summary>
/// Lectura del estado actual del cruce. Lo implementa el mismo controlador
/// de hardware porque es quien recibe las lineas EST: del ESP32.
/// </summary>
public interface IEstadoCruceProvider
{
    EstadoCruce ObtenerEstado();
}
