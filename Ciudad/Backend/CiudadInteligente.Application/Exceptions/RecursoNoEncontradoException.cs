namespace CiudadInteligente.Application.Exceptions;

/// <summary>Lo que se pidio no existe (p. ej. un usuario borrado). La API la convierte en 404.</summary>
public class RecursoNoEncontradoException(string mensaje) : Exception(mensaje);
