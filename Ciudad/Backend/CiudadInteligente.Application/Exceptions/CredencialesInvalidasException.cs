namespace CiudadInteligente.Application.Exceptions;

/// <summary>Usuario o contrasena incorrectos. La API la convierte en 401.</summary>
public class CredencialesInvalidasException(string mensaje = "Usuario o contraseña incorrectos.") : Exception(mensaje);
