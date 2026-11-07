using CiudadInteligente.Application.DTOs;
using CiudadInteligente.Application.Exceptions;
using CiudadInteligente.Application.Interfaces;
using CiudadInteligente.Application.Mapping;
using CiudadInteligente.Domain.Entities;
using CiudadInteligente.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace CiudadInteligente.Application.Services;

public class TraficoService(
    ISemaforoController semaforo,
    IConteoTraficoRepository repositorio,
    ILogger<TraficoService> logger) : ITraficoService
{
    public async Task RegistrarConteoAsync(RegistrarConteoDto dto)
    {
        if (dto.Vehiculos is null || dto.Vehiculos.Count != AccesoReglas.Cantidad)
        {
            throw new ReglaNegocioException($"Se esperaban {AccesoReglas.Cantidad} valores: Norte, Sur, Este y Oeste.");
        }

        if (dto.Vehiculos.Any(v => v < 0))
        {
            throw new ReglaNegocioException("El conteo de vehiculos no puede ser negativo.");
        }

        var conteo = ConteoTrafico.Desde(dto.Vehiculos, DateTimeOffset.UtcNow);

        // primero al cruce (afecta los tiempos de verde), luego al historial
        await semaforo.ActualizarTraficoAsync(conteo);

        try
        {
            await repositorio.GuardarAsync(conteo);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo guardar el conteo de trafico en la base de datos");
        }
    }

    public async Task<IReadOnlyList<ConteoTraficoDto>> ObtenerHistorialAsync(int cantidad)
    {
        var lista = await repositorio.ObtenerRecientesAsync(Math.Clamp(cantidad, 1, 500));
        return lista.Select(c => c.ADto()).ToList();
    }
}
