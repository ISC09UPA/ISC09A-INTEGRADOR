using InvernaderoInteligente.Application.DTOs;
using InvernaderoInteligente.Domain.Entities;

namespace InvernaderoInteligente.Application.Mappings;

public static class TelemetriaMapper
{
    public static LecturaSensor ToEntity(this CrearLecturaDto dto) => new()
    {
        SensorId = dto.SensorId.Trim(),
        Tipo = dto.Tipo.Trim().ToLowerInvariant(),
        Valor = dto.Valor!.Value,
        Unidad = dto.Unidad.Trim(),
        InvernaderoId = dto.InvernaderoId,
        FechaHora = (dto.FechaHora ?? DateTime.UtcNow).ToUniversalTime()
    };

    public static LecturaDto ToDto(this LecturaSensor lectura) => new()
    {
        Id = lectura.Id,
        SensorId = lectura.SensorId,
        Tipo = lectura.Tipo,
        Valor = lectura.Valor,
        Unidad = lectura.Unidad,
        InvernaderoId = lectura.InvernaderoId,
        FechaHora = DateTime.SpecifyKind(lectura.FechaHora, DateTimeKind.Utc)
    };

    public static Riego ToEntity(this CrearRiegoDto dto) => new()
    {
        InvernaderoId = dto.InvernaderoId,
        Zona = dto.Zona?.Trim(),
        FechaHoraInicio = (dto.FechaHoraInicio ?? DateTime.UtcNow).ToUniversalTime(),
        DuracionSegundos = dto.DuracionSegundos!.Value,
        VolumenMl = dto.VolumenMl,
        Origen = dto.Origen.Trim().ToLowerInvariant(),
        HumedadDetectada = dto.HumedadDetectada,
        Umbral = dto.Umbral,
        LecturaSensorId = dto.LecturaSensorId
    };

    public static RiegoDto ToDto(this Riego riego) => new()
    {
        Id = riego.Id,
        InvernaderoId = riego.InvernaderoId,
        Zona = riego.Zona,
        FechaHoraInicio = DateTime.SpecifyKind(riego.FechaHoraInicio, DateTimeKind.Utc),
        DuracionSegundos = riego.DuracionSegundos,
        VolumenMl = riego.VolumenMl,
        Origen = riego.Origen,
        HumedadDetectada = riego.HumedadDetectada,
        Umbral = riego.Umbral,
        LecturaSensorId = riego.LecturaSensorId
    };
}
