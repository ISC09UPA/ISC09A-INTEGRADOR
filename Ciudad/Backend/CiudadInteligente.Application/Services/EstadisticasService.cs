using CiudadInteligente.Application.DTOs;
using CiudadInteligente.Application.Interfaces;
using CiudadInteligente.Domain.Entities;
using CiudadInteligente.Domain.Enums;
using CiudadInteligente.Domain.Reglas;

namespace CiudadInteligente.Application.Services;

/// <summary>
/// Convierte el historial de conteos (camara o simulador) en lo que pide la
/// rubrica: flujo vehicular por hora y por dia, horas pico, mejor hora para
/// pasar, trafico actual contra lo normal, prediccion de congestion a corto
/// plazo y el comparativo de espera del temporizador fijo contra el adaptativo.
/// Todo se calcula en memoria con los conteos del periodo; con un conteo cada
/// 10 s son ~8,600 filas por dia, poco para la PC o para App Service.
/// </summary>
public class EstadisticasService(
    IConteoTraficoRepository repositorio,
    IEstadoCruceProvider estadoCruce,
    OpcionesEstadisticas opciones) : IEstadisticasService
{
    private static readonly int[] HorizontesMinutos = [15, 30, 60];
    private static readonly string[] NombresDias = ["domingo", "lunes", "martes", "miercoles", "jueves", "viernes", "sabado"];

    /// <summary>La "mejor hora para pasar" se busca solo de dia (de madrugada siempre hay poco).</summary>
    private const int PrimeraHoraDelDia = 6;
    private const int UltimaHoraDelDia = 22;

    /// <summary>Para el comparativo basta con una muestra pareja de este tamano.</summary>
    private const int MaximoMuestrasComparativo = 3000;

    public async Task<ResumenEstadisticasDto> ObtenerResumenAsync(int dias)
    {
        dias = Math.Clamp(dias, 1, 30);
        var ahora = DateTimeOffset.UtcNow;
        var conteos = await repositorio.ObtenerDesdeAsync(ahora.AddDays(-dias));
        var vivo = estadoCruce.ObtenerEstado().VehiculosPorAcceso;

        var ahoraLocal = ALocal(ahora);
        var porHora = CalcularFlujoPorHora(conteos);
        var perfil = porHora.Select(h => h.Muestras > 0 ? h.Promedio : (double?)null).ToArray();

        var actualReciente = PromedioReciente(conteos, ahora) ?? vivo.Sum();
        var normalAhora = PerfilEn(perfil, ahoraLocal);

        return new ResumenEstadisticasDto(
            ahora,
            opciones.Zona.Id,
            dias,
            conteos.Count,
            CalcularActual(vivo, normalAhora),
            porHora,
            CalcularFlujoPorDia(conteos),
            porHora.Where(h => h.Muestras > 0).OrderByDescending(h => h.Promedio).Take(3).Select(h => h.Hora).ToList(),
            MejorHora(porHora, ahoraLocal.Hour, proxima: false),
            MejorHora(porHora, ahoraLocal.Hour, proxima: true),
            Predecir(perfil, actualReciente, ahoraLocal),
            Comparar(conteos, vivo));
    }

    // ------------------------------------------------------------------
    // Flujo por hora / dia
    // ------------------------------------------------------------------

    private List<FlujoHoraDto> CalcularFlujoPorHora(IReadOnlyList<ConteoTrafico> conteos)
    {
        var grupos = conteos.ToLookup(c => ALocal(c.RegistradoEn).Hour);
        var resultado = new List<FlujoHoraDto>(24);

        for (var hora = 0; hora < 24; hora++)
        {
            var muestras = grupos[hora].ToList();
            if (muestras.Count == 0)
            {
                resultado.Add(new FlujoHoraDto(hora, 0, [0, 0, 0, 0], 0, 0, null));
                continue;
            }

            var promedio = muestras.Average(c => c.Total);
            resultado.Add(new FlujoHoraDto(
                hora,
                Redondear(promedio),
                [
                    Redondear(muestras.Average(c => c.Norte)),
                    Redondear(muestras.Average(c => c.Sur)),
                    Redondear(muestras.Average(c => c.Este)),
                    Redondear(muestras.Average(c => c.Oeste))
                ],
                muestras.Max(c => c.Total),
                muestras.Count,
                NivelDeTotal(promedio)));
        }

        return resultado;
    }

    private List<FlujoDiaDto> CalcularFlujoPorDia(IReadOnlyList<ConteoTrafico> conteos)
    {
        var grupos = conteos.ToLookup(c => (int)ALocal(c.RegistradoEn).DayOfWeek);
        return Enumerable.Range(0, 7)
            .Select(dia =>
            {
                var muestras = grupos[dia].ToList();
                var promedio = muestras.Count > 0 ? muestras.Average(c => c.Total) : 0;
                return new FlujoDiaDto(dia, NombresDias[dia], Redondear(promedio), muestras.Count);
            })
            .ToList();
    }

    private static MejorHoraDto? MejorHora(IReadOnlyList<FlujoHoraDto> porHora, int horaActual, bool proxima)
    {
        IEnumerable<(FlujoHoraDto Flujo, int? EnHoras)> candidatas = proxima
            ? Enumerable.Range(1, 6).Select(n => (porHora[(horaActual + n) % 24], (int?)n))
            : porHora.Where(h => h.Hora is >= PrimeraHoraDelDia and <= UltimaHoraDelDia).Select(h => (h, (int?)null));

        return candidatas
            .Where(c => c.Flujo.Muestras > 0)
            .OrderBy(c => c.Flujo.Promedio)
            .ThenBy(c => c.EnHoras ?? c.Flujo.Hora)
            .Select(c => new MejorHoraDto(c.Flujo.Hora, c.Flujo.Promedio, c.Flujo.Nivel ?? "bajo", c.EnHoras))
            .FirstOrDefault();
    }

    // ------------------------------------------------------------------
    // Trafico actual
    // ------------------------------------------------------------------

    private static TraficoActualDto CalcularActual(IReadOnlyList<int> vivo, double? normalAhora)
    {
        var total = vivo.Sum();
        var accesos = vivo
            .Select((v, i) => new AccesoTraficoDto(
                i + 1,
                AccesoReglas.Nombre(i + 1),
                v,
                NivelTraficoReglas.Calcular(v).ToString().ToLowerInvariant()))
            .ToList();

        string? comparado = null;
        if (normalAhora is { } normal)
        {
            comparado = total > normal * 1.2 ? "mas" : total < normal * 0.8 ? "menos" : "normal";
        }

        return new TraficoActualDto(
            total,
            NivelDeTotal(total),
            accesos,
            normalAhora is { } n ? Redondear(n) : null,
            comparado);
    }

    /// <summary>Promedio de los ultimos 10 minutos (el ultimo conteo solo puede ser un pico suelto).</summary>
    private static double? PromedioReciente(IReadOnlyList<ConteoTrafico> conteos, DateTimeOffset ahora)
    {
        var desde = ahora.AddMinutes(-10);
        var recientes = conteos.Where(c => c.RegistradoEn >= desde).ToList();
        return recientes.Count > 0 ? recientes.Average(c => c.Total) : null;
    }

    // ------------------------------------------------------------------
    // Prediccion a corto plazo
    // ------------------------------------------------------------------

    /// <summary>
    /// Mezcla dos cosas: lo que hay ahora (pesa mas a 15 min) y el perfil
    /// historico de la hora destino, escalado por que tan cargado esta hoy
    /// contra lo normal (pesa mas a 60 min). Peso del presente = 0.5^(min/30).
    /// </summary>
    private PrediccionDto Predecir(double?[] perfil, double actual, DateTimeOffset ahoraLocal)
    {
        var normalAhora = PerfilEn(perfil, ahoraLocal);
        var factorHoy = normalAhora is > 0 ? Math.Clamp(actual / normalAhora.Value, 0.3, 3.0) : 1.0;

        var puntos = new List<PuntoPrediccionDto>(HorizontesMinutos.Length);
        foreach (var minutos in HorizontesMinutos)
        {
            var destino = ahoraLocal.AddMinutes(minutos);
            var historico = PerfilEn(perfil, destino);
            var pesoPresente = Math.Pow(0.5, minutos / 30.0);
            var estimado = historico is { } h
                ? pesoPresente * actual + (1 - pesoPresente) * h * factorHoy
                : actual;

            var tendencia = estimado > actual * 1.1 ? "sube" : estimado < actual * 0.9 ? "baja" : "estable";
            puntos.Add(new PuntoPrediccionDto(minutos, destino.ToString("HH:mm"), Redondear(estimado), NivelDeTotal(estimado), tendencia));
        }

        var congestion = puntos.FirstOrDefault(p => p.Nivel == "alto");
        var aviso = congestion is null
            ? null
            : $"Se espera tráfico alto en {congestion.Minutos} min (~{Math.Round(congestion.TotalEstimado)} vehículos).";

        return new PrediccionDto(
            Redondear(actual),
            puntos,
            congestion is not null,
            aviso,
            perfil.Any(p => p is not null)
                ? "Tráfico de los últimos 10 min combinado con el promedio histórico de cada hora."
                : "Aún no hay historial: se repite el tráfico actual.");
    }

    /// <summary>Valor del perfil a una hora y minuto (interpola entre la hora y la siguiente).</summary>
    private static double? PerfilEn(double?[] perfil, DateTimeOffset local)
    {
        var a = perfil[local.Hour];
        var b = perfil[(local.Hour + 1) % 24];
        var fraccion = local.Minute / 60.0;
        return (a, b) switch
        {
            ({ } x, { } y) => x + (y - x) * fraccion,
            ({ } x, null) => x,
            (null, { } y) => y,
            _ => null
        };
    }

    // ------------------------------------------------------------------
    // Fijo vs. adaptativo
    // ------------------------------------------------------------------

    private static ComparativoDto Comparar(IReadOnlyList<ConteoTrafico> conteos, IReadOnlyList<int> vivo)
    {
        var muestras = Submuestra(conteos.Where(c => c.Total > 0).ToList(), MaximoMuestrasComparativo);

        double fijoPonderado = 0, adaptativoPonderado = 0, cicloFijo = 0, cicloAdaptativo = 0;
        long vehiculos = 0;
        var fijoAcceso = new double[AccesoReglas.Cantidad];
        var adaptativoAcceso = new double[AccesoReglas.Cantidad];
        var vehiculosAcceso = new long[AccesoReglas.Cantidad];

        foreach (var conteo in muestras)
        {
            var valores = conteo.ComoArreglo();
            var fijo = ModeloEspera.Calcular(valores, adaptativo: false);
            var adaptativo = ModeloEspera.Calcular(valores, adaptativo: true);

            fijoPonderado += fijo.PromedioSegundos * fijo.Vehiculos;
            adaptativoPonderado += adaptativo.PromedioSegundos * adaptativo.Vehiculos;
            vehiculos += fijo.Vehiculos;
            cicloFijo += fijo.CicloSegundos;
            cicloAdaptativo += adaptativo.CicloSegundos;

            for (var i = 0; i < valores.Length; i++)
            {
                fijoAcceso[i] += fijo.PorAccesoSegundos[i] * valores[i];
                adaptativoAcceso[i] += adaptativo.PorAccesoSegundos[i] * valores[i];
                vehiculosAcceso[i] += valores[i];
            }
        }

        var esperaFijo = vehiculos > 0 ? fijoPonderado / vehiculos : 0;
        var esperaAdaptativo = vehiculos > 0 ? adaptativoPonderado / vehiculos : 0;

        var porAcceso = Enumerable.Range(0, AccesoReglas.Cantidad)
            .Select(i => new ComparativoAccesoDto(
                i + 1,
                AccesoReglas.Nombre(i + 1),
                Redondear(vehiculosAcceso[i] > 0 ? fijoAcceso[i] / vehiculosAcceso[i] : 0),
                Redondear(vehiculosAcceso[i] > 0 ? adaptativoAcceso[i] / vehiculosAcceso[i] : 0)))
            .ToList();

        var fijoAhora = ModeloEspera.Calcular(vivo, adaptativo: false);
        var adaptativoAhora = ModeloEspera.Calcular(vivo, adaptativo: true);

        return new ComparativoDto(
            Redondear(esperaFijo),
            Redondear(esperaAdaptativo),
            Redondear(esperaFijo > 0 ? (esperaFijo - esperaAdaptativo) / esperaFijo * 100 : 0),
            Redondear(muestras.Count > 0 ? cicloFijo / muestras.Count : fijoAhora.CicloSegundos),
            Redondear(muestras.Count > 0 ? cicloAdaptativo / muestras.Count : adaptativoAhora.CicloSegundos),
            muestras.Count,
            vehiculos,
            porAcceso,
            new ComparativoAccesoDto(0, "Ahora", Redondear(fijoAhora.PromedioSegundos), Redondear(adaptativoAhora.PromedioSegundos)),
            "Retraso uniforme de Webster (rojo² / 2·ciclo) más un ciclo extra para los vehículos que no caben en el verde; tiempos de la maqueta.");
    }

    /// <summary>Toma muestras parejas a lo largo del periodo si son demasiadas.</summary>
    private static List<ConteoTrafico> Submuestra(List<ConteoTrafico> conteos, int maximo)
    {
        if (conteos.Count <= maximo) return conteos;
        var paso = (double)conteos.Count / maximo;
        return Enumerable.Range(0, maximo).Select(i => conteos[(int)(i * paso)]).ToList();
    }

    // ------------------------------------------------------------------

    private DateTimeOffset ALocal(DateTimeOffset momento) => TimeZoneInfo.ConvertTime(momento, opciones.Zona);

    /// <summary>Nivel del cruce: el nivel de un acceso promedio (mismos limites que la camara).</summary>
    private static string NivelDeTotal(double total) =>
        NivelTraficoReglas.Calcular((int)Math.Round(total / AccesoReglas.Cantidad)).ToString().ToLowerInvariant();

    private static double Redondear(double valor) => Math.Round(valor, 1);
}
