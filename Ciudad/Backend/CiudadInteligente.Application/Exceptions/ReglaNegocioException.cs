namespace CiudadInteligente.Application.Exceptions;

/// <summary>
/// La peticion no cumple una regla del cruce (acceso invalido, verdes en
/// conflicto, datos incompletos...). La API la convierte en 400 Bad Request.
/// </summary>
public class ReglaNegocioException(string mensaje) : Exception(mensaje);
