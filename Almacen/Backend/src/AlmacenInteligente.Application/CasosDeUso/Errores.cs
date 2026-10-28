namespace AlmacenInteligente.Application.CasosDeUso;

// Los adaptadores traducen estas excepciones: la API a 401/403/404/422 y Blazor a mensajes en pantalla.
// Las reglas de negocio violadas llegan como DomainException (409/422 según el caso).

public sealed class NoAutenticadoException() : Exception("Debes iniciar sesión.");

public sealed class AccesoDenegadoException(string permiso) : Exception("No tienes permiso para realizar esta acción.")
{
    public string Permiso { get; } = permiso;
}

public sealed class NoEncontradoException(string recurso, Guid id) : Exception($"No se encontró {recurso}.")
{
    public string Recurso { get; } = recurso;
    public Guid Id { get; } = id;
}

public sealed class ValidacionException(IReadOnlyDictionary<string, string[]> errores)
    : Exception("Hay datos inválidos en la solicitud.")
{
    public IReadOnlyDictionary<string, string[]> Errores { get; } = errores;
}
