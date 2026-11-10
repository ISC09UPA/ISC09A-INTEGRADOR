namespace CiudadInteligente.Domain.Reglas;

/// <summary>
/// Tiempos del ciclo automatico, los mismos del firmware
/// (Embebidos/ESP32/semaforo_v2/semaforo_v2.ino). Los usan el backend para
/// seguir el ciclo en vivo (Infrastructure/Hardware/TiemposFirmware) y las
/// estadisticas para comparar el modo fijo contra el adaptativo.
/// Si cambias un tiempo en el .ino, cambialo aqui.
/// </summary>
public static class TiemposSemaforo
{
    public const int TodosRojoMs = 500;        // DUR_LAP_INICIO
    public const int VerdeFijoMs = 2500;       // DUR_VERDE
    public const int VerdeMinimoMs = 1500;     // DUR_VERDE_MIN
    public const int VerdeMaximoMs = 5000;     // DUR_VERDE_MAX
    public const int ExtraPorVehiculoMs = 300; // extraPorVehiculo
    public const int ParpadeoMs = 200 * 6;     // DUR_PARPADEO_FASE x 6 cambios
    public const int AmarilloMs = 1000;        // DUR_AMARILLO
    public const int RojoFinMs = 500;          // DUR_ROJO_FIN

    /// <summary>Lo que tarda un acceso despues de su verde: parpadeo + amarillo + rojo final.</summary>
    public const int CierreDeTurnoMs = ParpadeoMs + AmarilloMs + RojoFinMs;

    /// <summary>Igual que calcularDuracionVerde() del firmware (modo adaptativo).</summary>
    public static int DuracionVerde(int vehiculos)
    {
        if (vehiculos <= 0) return VerdeFijoMs;
        return Math.Clamp(VerdeMinimoMs + vehiculos * ExtraPorVehiculoMs, VerdeMinimoMs, VerdeMaximoMs);
    }

    /// <summary>Un ciclo completo: todos en rojo y luego el turno de cada acceso.</summary>
    public static int DuracionCicloMs(IReadOnlyList<int> verdesMs) =>
        TodosRojoMs + verdesMs.Sum(verde => verde + CierreDeTurnoMs);
}
