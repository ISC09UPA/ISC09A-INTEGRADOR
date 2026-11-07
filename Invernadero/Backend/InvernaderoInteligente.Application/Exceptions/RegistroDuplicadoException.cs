namespace InvernaderoInteligente.Application.Exceptions;

/// <summary>
/// Se lanza cuando se intenta guardar una lectura o un riego que ya existe.
/// La API la convierte en 409 Conflict (ver API/Middleware/ManejadorErrores.cs);
/// la Azure Function interpreta el 409 como "ya estaba guardado".
/// </summary>
public class RegistroDuplicadoException(string message) : Exception(message);
