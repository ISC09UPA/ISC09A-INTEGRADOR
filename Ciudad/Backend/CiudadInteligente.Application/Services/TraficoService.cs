using CiudadInteligente.Application.DTOs;
using CiudadInteligente.Application.Exceptions;
using CiudadInteligente.Application.Interfaces;
using CiudadInteligente.Application.Mapping;
using CiudadInteligente.Domain.Entities;
using CiudadInteligente.Domain.Enums;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace CiudadInteligente.Application.Services;

public class TraficoService(
    ISemaforoController semaforo,
    IConteoTraficoRepository repositorio,
    ILogger<TraficoService> logger) : ITraficoService
{
    /// <summary>Tope por peticion de POST api/trafico/lote.</summary>
    public const int MaximoPorLote = 5000;

    /// <summary>Mas vehiculos que esto en un acceso es un error de la camara.</summary>
    private const int MaximoPorAcceso = 500;

    /// <summary>Hasta cuantos dias atras se acepta historial.</summary>
    private const int DiasMaximosHistorial = 60;

    public async Task<bool> RegistrarConteoAsync(RegistrarConteoDto dto)
    {
        Validar(dto.Vehiculos);
        var conteo = ConteoTrafico.Desde(dto.Vehiculos, DateTimeOffset.UtcNow);

        // primero al cruce (afecta los tiempos de verde), luego al historial.
        // Si el ESP32 no esta disponible el conteo igual se guarda: la
        // estadistica no debe perder datos porque la Raspberry este apagada.
        try
        {
            await semaforo.ActualizarTraficoAsync(conteo);
        }
        catch (HardwareNoDisponibleException ex)
        {
            logger.LogWarning("Conteo guardado sin mandarlo al ESP32: {Mensaje}", ex.Message);
        }

        try
        {
            await repositorio.GuardarAsync(conteo);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo guardar el conteo de trafico en la base de datos");
            return false;
        }
    }

    public async Task<int> RegistrarLoteAsync(RegistrarLoteConteosDto dto)
    {
        if (dto.Conteos is null || dto.Conteos.Count == 0)
        {
            throw new ReglaNegocioException("El lote no trae conteos.");
        }

        if (dto.Conteos.Count > MaximoPorLote)
        {
            throw new ReglaNegocioException($"Máximo {MaximoPorLote} conteos por lote; manda varios lotes.");
        }

        var ahora = DateTimeOffset.UtcNow;
        var limiteViejo = ahora.AddDays(-DiasMaximosHistorial);
        var conteos = new List<ConteoTrafico>(dto.Conteos.Count);

        foreach (var item in dto.Conteos)
        {
            Validar(item.Vehiculos);
            if (item.RegistradoEn > ahora.AddMinutes(1) || item.RegistradoEn < limiteViejo)
            {
                throw new ReglaNegocioException(
                    $"La fecha {item.RegistradoEn:O} está fuera de rango (de hace {DiasMaximosHistorial} días hasta ahora).");
            }

            conteos.Add(ConteoTrafico.Desde(item.Vehiculos, item.RegistradoEn.ToUniversalTime()));
        }

        await repositorio.GuardarVariosAsync(conteos);
        logger.LogInformation("Lote de historial guardado: {Cantidad} conteos", conteos.Count);
        return conteos.Count;
    }

    public async Task<IReadOnlyList<ConteoTraficoDto>> ObtenerHistorialAsync(int cantidad)
    {
        var lista = await repositorio.ObtenerRecientesAsync(Math.Clamp(cantidad, 1, 500));
        return lista.Select(c => c.ADto()).ToList();
    }

    private static void Validar([NotNull] IReadOnlyList<int>? vehiculos)
    {
        if (vehiculos is null || vehiculos.Count != AccesoReglas.Cantidad)
        {
            throw new ReglaNegocioException($"Se esperaban {AccesoReglas.Cantidad} valores: Norte, Sur, Este y Oeste.");
        }

        if (vehiculos.Any(v => v < 0))
        {
            throw new ReglaNegocioException("El conteo de vehiculos no puede ser negativo.");
        }

        if (vehiculos.Any(v => v > MaximoPorAcceso))
        {
            throw new ReglaNegocioException($"Más de {MaximoPorAcceso} vehículos en un acceso: revisa la cámara.");
        }
    }
}
