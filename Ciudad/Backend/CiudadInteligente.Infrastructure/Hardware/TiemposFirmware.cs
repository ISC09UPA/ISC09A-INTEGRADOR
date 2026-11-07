namespace CiudadInteligente.Infrastructure.Hardware;

/// <summary>
/// Los mismos tiempos que usa el firmware (Embebidos/ESP32/semaforo_v2/semaforo_v2.ino).
/// Con ellos el backend calcula en que fase va el ciclo automatico entre una
/// linea EST: y la siguiente. Con IoT Hub la Raspberry solo reporta el inicio
/// de cada ciclo (para no gastar el limite de mensajes), asi que esto es lo que
/// mantiene la app en vivo. Si cambias un tiempo en el .ino, cambialo aqui.
/// </summary>
public static class TiemposFirmware
{
    public const int TodosRojoMs = 500;        // DUR_LAP_INICIO
    public const int VerdeFijoMs = 2500;       // DUR_VERDE
    public const int VerdeMinimoMs = 1500;     // DUR_VERDE_MIN
    public const int VerdeMaximoMs = 5000;     // DUR_VERDE_MAX
    public const int ExtraPorVehiculoMs = 300; // extraPorVehiculo
    public const int ParpadeoMs = 200 * 6;     // DUR_PARPADEO_FASE x 6 cambios
    public const int AmarilloMs = 1000;        // DUR_AMARILLO
    public const int RojoFinMs = 500;          // DUR_ROJO_FIN

    /// <summary>Igual que calcularDuracionVerde() del firmware.</summary>
    public static int DuracionVerde(int vehiculos)
    {
        if (vehiculos <= 0) return VerdeFijoMs;
        return Math.Clamp(VerdeMinimoMs + vehiculos * ExtraPorVehiculoMs, VerdeMinimoMs, VerdeMaximoMs);
    }
}
