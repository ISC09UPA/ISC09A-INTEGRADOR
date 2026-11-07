using CiudadInteligente.Application.DTOs;
using CiudadInteligente.Domain.Entities;
using CiudadInteligente.Domain.Enums;
using CiudadInteligente.Domain.Models;

namespace CiudadInteligente.Application.Mapping;

/// <summary>Conversion entre entidades/modelos del dominio y los DTOs de la API.</summary>
public static class CruceMapper
{
    public static EstadoCruceDto ADto(this EstadoCruce estado, DateTimeOffset ahora)
    {
        var accesos = new List<AccesoEstadoDto>(AccesoReglas.Cantidad);
        for (var i = 0; i < AccesoReglas.Cantidad; i++)
        {
            var numero = i + 1;
            var vehiculos = estado.VehiculosPorAcceso[i];
            accesos.Add(new AccesoEstadoDto(
                numero,
                AccesoReglas.Nombre(numero),
                ColorATexto(estado.Colores[i]),
                vehiculos,
                NivelTraficoReglas.Calcular(vehiculos).ToString().ToLowerInvariant()));
        }

        var transcurridos = (int)Math.Clamp((ahora - estado.FaseIniciadaEn).TotalMilliseconds, 0, int.MaxValue);

        return new EstadoCruceDto(
            estado.Modo.ToString().ToLowerInvariant(),
            estado.Adaptativo,
            estado.Esp32Conectado,
            estado.AccesoEnTurno,
            estado.Fase,
            estado.DuracionFaseMs,
            transcurridos,
            accesos,
            estado.VehiculosPorAcceso.Sum(),
            estado.AccesoAmbulancia,
            estado.ActualizadoEn);
    }

    public static EmergenciaDto ADto(this Emergencia e, bool registrada = true) => new(
        e.Id,
        e.Unidad,
        e.Acceso,
        AccesoReglas.Nombre(e.Acceso),
        e.Tipo.ToString().ToLowerInvariant(),
        e.EtaMinutos,
        e.SolicitadaEn,
        e.FinalizadaEn,
        e.EstaActiva,
        registrada);

    public static ConteoTraficoDto ADto(this ConteoTrafico c) =>
        new(c.Id, c.RegistradoEn, c.Norte, c.Sur, c.Este, c.Oeste, c.Total);

    public static string? ColorATexto(ColorSemaforo? color) => color?.ToString().ToLowerInvariant();
}
