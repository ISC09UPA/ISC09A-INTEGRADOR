using CiudadInteligente.Application.DTOs;
using CiudadInteligente.Application.Exceptions;
using CiudadInteligente.Application.Interfaces;
using CiudadInteligente.Application.Mapping;
using CiudadInteligente.Domain.Entities;
using CiudadInteligente.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace CiudadInteligente.Application.Services;

public class EmergenciaService(
    ISemaforoController semaforo,
    IEmergenciaRepository repositorio,
    ILogger<EmergenciaService> logger) : IEmergenciaService
{
    private const int LargoMaximoUnidad = 80;
    private const int EtaMaximaMinutos = 120;

    public async Task<EmergenciaDto> SolicitarAsync(SolicitudEmergenciaDto dto)
    {
        var unidad = dto.Unidad?.Trim() ?? string.Empty;
        if (unidad.Length == 0)
        {
            throw new ReglaNegocioException("Escribe la unidad que solicita el paso (ej. AMB-042 Cruz Roja).");
        }

        if (unidad.Length > LargoMaximoUnidad)
        {
            throw new ReglaNegocioException($"El nombre de la unidad no puede pasar de {LargoMaximoUnidad} caracteres.");
        }

        if (!AccesoReglas.EsValido(dto.Acceso))
        {
            throw new ReglaNegocioException("La direccion de llegada debe ser 1 (Norte), 2 (Sur), 3 (Este) o 4 (Oeste).");
        }

        if (!Enum.TryParse<TipoEmergencia>(dto.Tipo, ignoreCase: true, out var tipo) || !Enum.IsDefined(tipo))
        {
            throw new ReglaNegocioException("Tipo de emergencia invalido: usa medica, incendio o seguridad.");
        }

        if (dto.EtaMinutos is < 0 or > EtaMaximaMinutos)
        {
            throw new ReglaNegocioException($"El tiempo estimado debe estar entre 0 y {EtaMaximaMinutos} minutos.");
        }

        // Seguridad primero: el corredor se activa ANTES de intentar guardar.
        // Si la base de datos falla, la ambulancia igual tiene paso.
        await semaforo.ActivarAmbulanciaAsync(dto.Acceso);

        var emergencia = new Emergencia
        {
            Unidad = unidad,
            Acceso = dto.Acceso,
            Tipo = tipo,
            EtaMinutos = dto.EtaMinutos,
            SolicitadaEn = DateTimeOffset.UtcNow
        };

        try
        {
            emergencia.Id = await repositorio.RegistrarAsync(emergencia);
            return emergencia.ADto(registrada: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "El corredor se activo pero no se pudo registrar la emergencia de {Unidad}", unidad);
            return emergencia.ADto(registrada: false);
        }
    }

    public async Task FinalizarAsync()
    {
        await semaforo.FinalizarAmbulanciaAsync();

        try
        {
            await repositorio.FinalizarActivasAsync(DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "El corredor termino pero no se pudo actualizar el registro en la base de datos");
        }
    }

    public async Task<IReadOnlyList<EmergenciaDto>> ObtenerRecientesAsync(int cantidad)
    {
        var lista = await repositorio.ObtenerRecientesAsync(Math.Clamp(cantidad, 1, 50));
        return lista.Select(e => e.ADto()).ToList();
    }
}
