namespace CiudadInteligente.Application.Interfaces;

/// <summary>
/// Puerto para mandar avisos por correo (Azure Communication Services Email) a
/// los destinatarios configurados. Nunca lanza: si el correo falla solo se
/// registra, porque el cruce no puede depender del correo.
/// </summary>
public interface IServicioCorreo
{
    Task NotificarAsync(string asunto, string mensaje, CancellationToken cancelacion = default);
}
