using CiudadInteligente.Domain.Enums;

namespace CiudadInteligente.Domain.Reglas;

/// <summary>Espera estimada en un acceso y en todo el cruce para un conteo de vehiculos.</summary>
/// <param name="PromedioSegundos">Espera promedio por vehiculo en todo el cruce (ponderada por vehiculos).</param>
/// <param name="PorAccesoSegundos">Indice 0 = acceso 1.</param>
/// <param name="CicloSegundos">Duracion del ciclo completo con esos tiempos de verde.</param>
/// <param name="Vehiculos">Total de vehiculos del conteo.</param>
public record ResultadoEspera(
    double PromedioSegundos,
    IReadOnlyList<double> PorAccesoSegundos,
    double CicloSegundos,
    int Vehiculos);

/// <summary>
/// Cuanto espera en rojo un vehiculo segun los tiempos del semaforo. Sirve
/// para comparar el temporizador fijo contra el adaptativo con los MISMOS
/// conteos de la camara (lo que pide la rubrica).
///
/// Modelo (retraso uniforme de Webster, version sencilla):
///   - Verde efectivo G = verde + parpadeo (con el verde parpadeando aun se pasa).
///   - Rojo efectivo  R = ciclo - G.
///   - Un vehiculo que llega en un momento al azar espera en promedio R^2 / (2 * ciclo).
///   - En un verde caben G / 300 ms vehiculos (lo que tarda en cruzar cada coche de
///     la fila en la maqueta); los que no caben esperan un ciclo mas.
/// Los tiempos son los de la maqueta (el ciclo dura ~20 s, no ~2 min como en la calle).
/// </summary>
public static class ModeloEspera
{
    /// <summary>
    /// Tiempo que tarda en cruzar cada vehiculo de la fila (propiedad del cruce,
    /// no del algoritmo: no cambia aunque cambies extraPorVehiculo en el firmware).
    /// </summary>
    public const int MsPorVehiculo = 300;

    public static ResultadoEspera Calcular(IReadOnlyList<int> vehiculos, bool adaptativo)
    {
        if (vehiculos.Count != AccesoReglas.Cantidad)
        {
            throw new ArgumentException($"Se esperaban {AccesoReglas.Cantidad} valores (uno por acceso).", nameof(vehiculos));
        }

        var verdes = vehiculos
            .Select(v => adaptativo ? TiemposSemaforo.DuracionVerde(v) : TiemposSemaforo.VerdeFijoMs)
            .ToArray();
        double ciclo = TiemposSemaforo.DuracionCicloMs(verdes);

        var porAcceso = new double[vehiculos.Count];
        double esperaPonderada = 0;
        var total = 0;

        for (var i = 0; i < vehiculos.Count; i++)
        {
            double verdeEfectivo = verdes[i] + TiemposSemaforo.ParpadeoMs;
            var rojoEfectivo = ciclo - verdeEfectivo;
            var uniforme = rojoEfectivo * rojoEfectivo / (2 * ciclo);

            var capacidad = (int)(verdeEfectivo / MsPorVehiculo);
            var sobrantes = Math.Max(0, vehiculos[i] - capacidad);
            var espera = vehiculos[i] > 0 ? uniforme + (double)sobrantes / vehiculos[i] * ciclo : uniforme;

            porAcceso[i] = espera / 1000;
            esperaPonderada += vehiculos[i] * espera;
            total += vehiculos[i];
        }

        var promedio = total > 0 ? esperaPonderada / total / 1000 : 0;
        return new ResultadoEspera(promedio, porAcceso, ciclo / 1000, total);
    }
}
