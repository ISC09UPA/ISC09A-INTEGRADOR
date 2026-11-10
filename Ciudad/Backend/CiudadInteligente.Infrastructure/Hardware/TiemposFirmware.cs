using CiudadInteligente.Domain.Reglas;

namespace CiudadInteligente.Infrastructure.Hardware;

/// <summary>
/// Los tiempos del firmware, para que el backend calcule en que fase va el
/// ciclo automatico entre una linea EST: y la siguiente. Con IoT Hub la
/// Raspberry solo reporta el inicio de cada ciclo (para no gastar el limite de
/// mensajes), asi que esto es lo que mantiene la app en vivo.
/// Los valores viven en Domain/Reglas/TiemposSemaforo (tambien los usan las
/// estadisticas); si cambias un tiempo en el .ino, cambialo alla.
/// </summary>
public static class TiemposFirmware
{
    public const int TodosRojoMs = TiemposSemaforo.TodosRojoMs;
    public const int VerdeFijoMs = TiemposSemaforo.VerdeFijoMs;
    public const int VerdeMinimoMs = TiemposSemaforo.VerdeMinimoMs;
    public const int VerdeMaximoMs = TiemposSemaforo.VerdeMaximoMs;
    public const int ExtraPorVehiculoMs = TiemposSemaforo.ExtraPorVehiculoMs;
    public const int ParpadeoMs = TiemposSemaforo.ParpadeoMs;
    public const int AmarilloMs = TiemposSemaforo.AmarilloMs;
    public const int RojoFinMs = TiemposSemaforo.RojoFinMs;

    /// <summary>Igual que calcularDuracionVerde() del firmware.</summary>
    public static int DuracionVerde(int vehiculos) => TiemposSemaforo.DuracionVerde(vehiculos);
}
