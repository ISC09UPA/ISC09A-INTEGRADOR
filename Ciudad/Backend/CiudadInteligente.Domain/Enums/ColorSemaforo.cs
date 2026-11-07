namespace CiudadInteligente.Domain.Enums;

/// <summary>
/// Colores de un semaforo. Los nombres coinciden con lo que entiende el
/// firmware del ESP32 (se mandan en mayusculas: VERDE, AMARILLO, ROJO).
/// </summary>
public enum ColorSemaforo
{
    Verde,
    Amarillo,
    Rojo
}
