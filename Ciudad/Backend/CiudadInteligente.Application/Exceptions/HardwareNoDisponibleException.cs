namespace CiudadInteligente.Application.Exceptions;

/// <summary>
/// No hay comunicacion con el ESP32 (puerto serial cerrado o puente de la
/// Raspberry Pi apagado). La API la convierte en 503 Service Unavailable.
/// </summary>
public class HardwareNoDisponibleException(string mensaje, Exception? causa = null) : Exception(mensaje, causa);
