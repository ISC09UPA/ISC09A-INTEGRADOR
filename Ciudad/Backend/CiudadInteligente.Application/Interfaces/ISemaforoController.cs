using CiudadInteligente.Domain.Entities;
using CiudadInteligente.Domain.Enums;

namespace CiudadInteligente.Application.Interfaces;

/// <summary>
/// Puerto hacia el hardware del cruce (el ESP32). No sabe nada de serial,
/// TCP ni USB: eso vive en Infrastructure/Hardware. Si cambia el medio de
/// comunicacion solo se cambia la implementacion, nunca este contrato.
///   - SerialSemaforoController: ESP32 por USB en la misma maquina.
///   - RaspberryPiSemaforoController: ESP32 conectado a la Raspberry Pi,
///     comandos por TCP al puente Embebidos/RaspberryPi/puente_serial.
/// Si no hay comunicacion con el ESP32 lanza HardwareNoDisponibleException.
/// </summary>
public interface ISemaforoController
{
    Task SetModoAutoAsync();

    Task SetModoManualAsync();

    /// <param name="acceso">1 a 4.</param>
    Task SetColorAsync(int acceso, ColorSemaforo color);

    /// <summary>
    /// Varios colores de una sola vez (y, si se pide, el paso a manual antes).
    /// Se aplican en el orden recibido. Por IoT Hub viajan juntos en un solo
    /// mensaje, en vez de una ida y vuelta a Azure por cada acceso.
    /// </summary>
    Task AplicarColoresAsync(IReadOnlyList<(int Acceso, ColorSemaforo Color)> colores, bool entrarAManual);

    /// <param name="acceso">1 a 4: acceso que recibe el corredor de emergencia.</param>
    Task ActivarAmbulanciaAsync(int acceso);

    Task FinalizarAmbulanciaAsync();

    /// <summary>
    /// Conteo de vehiculos de la camara. En modo adaptativo se reenvia al
    /// ESP32 (verde segun densidad); en temporizador fijo solo se guarda para
    /// mostrarlo en la app.
    /// </summary>
    Task ActualizarTraficoAsync(ConteoTrafico conteo);

    /// <summary>
    /// Adaptativo = verde segun densidad de trafico; fijo = mismo verde para
    /// los 4 accesos. En ambos casos el ESP32 queda en MODO:AUTO.
    /// </summary>
    Task SetAdaptativoAsync(bool adaptativo);
}
