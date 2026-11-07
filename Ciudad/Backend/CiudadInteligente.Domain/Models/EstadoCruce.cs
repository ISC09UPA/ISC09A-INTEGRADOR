using CiudadInteligente.Domain.Enums;

namespace CiudadInteligente.Domain.Models;

/// <summary>
/// Fotografia del cruce en este momento: lo que reporta el ESP32 con sus
/// lineas EST: (fase del ciclo automatico) mas lo que el backend le ordeno
/// (modo, colores manuales, ambulancia) y el ultimo conteo de la camara.
/// </summary>
/// <param name="AccesoEnTurno">0 = todos en rojo, 1-4 = acceso con el turno.</param>
/// <param name="Fase">TODOS_ROJO, VERDE, PARPADEO, AMARILLO, ROJO_FIN, MANUAL, AMBULANCIA o DESCONOCIDA.</param>
/// <param name="Colores">Indice 0 = acceso 1. Null = LED apagado (p. ej. al entrar a manual).</param>
/// <param name="VehiculosPorAcceso">Indice 0 = acceso 1.</param>
public record EstadoCruce(
    ModoSemaforo Modo,
    bool Adaptativo,
    bool Esp32Conectado,
    int AccesoEnTurno,
    string Fase,
    int DuracionFaseMs,
    DateTimeOffset FaseIniciadaEn,
    IReadOnlyList<ColorSemaforo?> Colores,
    IReadOnlyList<int> VehiculosPorAcceso,
    int? AccesoAmbulancia,
    DateTimeOffset ActualizadoEn);
