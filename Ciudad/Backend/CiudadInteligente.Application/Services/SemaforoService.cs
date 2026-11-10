using CiudadInteligente.Application.DTOs;
using CiudadInteligente.Application.Exceptions;
using CiudadInteligente.Application.Interfaces;
using CiudadInteligente.Application.Mapping;
using CiudadInteligente.Domain.Enums;

namespace CiudadInteligente.Application.Services;

public class SemaforoService(ISemaforoController semaforo, IEstadoCruceProvider estado) : ISemaforoService
{
    public EstadoCruceDto ObtenerEstado() => estado.ObtenerEstado().ADto(DateTimeOffset.UtcNow);

    public async Task<EstadoCruceDto> CambiarModoAsync(CambiarModoDto dto)
    {
        switch (dto.Modo?.Trim().ToLowerInvariant())
        {
            case "adaptativo":
                await semaforo.SetAdaptativoAsync(true);
                break;
            case "fijo":
                await semaforo.SetAdaptativoAsync(false);
                break;
            case "manual":
                await semaforo.SetModoManualAsync();
                break;
            default:
                throw new ReglaNegocioException("Modo invalido. Usa \"adaptativo\", \"fijo\" o \"manual\".");
        }

        return ObtenerEstado();
    }

    public async Task<EstadoCruceDto> AplicarSecuenciaAsync(SecuenciaManualDto dto)
    {
        if (dto.Accesos is null || dto.Accesos.Count == 0)
        {
            throw new ReglaNegocioException("La secuencia no trae ningun acceso.");
        }

        var cambios = new Dictionary<int, ColorSemaforo>();
        foreach (var item in dto.Accesos)
        {
            if (!AccesoReglas.EsValido(item.Acceso))
            {
                throw new ReglaNegocioException($"Acceso {item.Acceso} invalido: debe ser 1 (Norte), 2 (Sur), 3 (Este) o 4 (Oeste).");
            }

            if (!Enum.TryParse<ColorSemaforo>(item.Color, ignoreCase: true, out var color) ||
                !Enum.IsDefined(color))
            {
                throw new ReglaNegocioException($"Color \"{item.Color}\" invalido: usa verde, amarillo o rojo.");
            }

            if (!cambios.TryAdd(item.Acceso, color))
            {
                throw new ReglaNegocioException($"El acceso {item.Acceso} viene repetido en la secuencia.");
            }
        }

        ValidarSinConflictos(cambios);

        var actual = estado.ObtenerEstado();

        // primero los rojos, luego el resto: nunca queda un instante con
        // dos ejes en verde mientras el ESP32 aplica los cambios uno por uno
        var ordenados = cambios
            .OrderBy(c => c.Value == ColorSemaforo.Rojo ? 0 : 1)
            .Select(c => (c.Key, c.Value))
            .ToList();

        await semaforo.AplicarColoresAsync(ordenados, entrarAManual: actual.Modo != ModoSemaforo.Manual);

        return ObtenerEstado();
    }

    public async Task<EstadoCruceDto> DetenerTodoAsync()
    {
        var todosEnRojo = Enumerable.Range(1, AccesoReglas.Cantidad)
            .Select(acceso => (acceso, ColorSemaforo.Rojo))
            .ToList();

        await semaforo.AplicarColoresAsync(todosEnRojo, entrarAManual: true);

        return ObtenerEstado();
    }

    /// <summary>
    /// Regla de seguridad del cruce: Norte-Sur y Este-Oeste no pueden tener
    /// paso (verde o amarillo) al mismo tiempo. Los accesos que no vienen en
    /// la secuencia conservan el color que ya tenian.
    /// </summary>
    private void ValidarSinConflictos(IReadOnlyDictionary<int, ColorSemaforo> cambios)
    {
        var actual = estado.ObtenerEstado();
        var resultado = new ColorSemaforo?[AccesoReglas.Cantidad];
        for (var i = 0; i < AccesoReglas.Cantidad; i++)
        {
            // fuera de manual el ESP32 deja todo apagado al entrar a manual
            resultado[i] = actual.Modo == ModoSemaforo.Manual ? actual.Colores[i] : null;
        }

        foreach (var (acceso, color) in cambios)
        {
            resultado[acceso - 1] = color;
        }

        static bool DaPaso(ColorSemaforo? c) => c is ColorSemaforo.Verde or ColorSemaforo.Amarillo;

        var pasoNorteSur = DaPaso(resultado[0]) || DaPaso(resultado[1]);
        var pasoEsteOeste = DaPaso(resultado[2]) || DaPaso(resultado[3]);

        if (pasoNorteSur && pasoEsteOeste)
        {
            throw new ReglaNegocioException(
                "Conflicto de seguridad: Norte-Sur y Este-Oeste no pueden tener verde o amarillo al mismo tiempo.");
        }
    }
}
