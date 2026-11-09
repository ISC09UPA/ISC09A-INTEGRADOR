namespace InvernaderoInteligente.Functions.Models;

/// <summary>
/// Error permanente: el mensaje nunca se va a poder guardar (JSON roto, datos inválidos,
/// la API respondió 4xx). El mensaje se manda directo a la dead-letter queue sin reintentos.
/// </summary>
public class MensajeInvalidoException(string message) : Exception(message);
