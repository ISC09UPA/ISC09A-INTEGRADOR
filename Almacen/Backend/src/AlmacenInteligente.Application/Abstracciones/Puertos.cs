namespace AlmacenInteligente.Application.Abstracciones;

/// <summary>El usuario que ejecuta el caso de uso, venga de la cookie de Blazor o del JWT de la API.</summary>
public interface IUsuarioActual
{
    Guid? Id { get; }
    bool EstaAutenticado { get; }
    bool TieneRol(string rol);
    bool TienePermiso(string permiso);
}

public interface IReloj
{
    DateTimeOffset Ahora { get; }
    DateOnly Hoy { get; }
}

/// <summary>Confirma en una sola transacción los cambios de los repositorios y los eventos del outbox.</summary>
public interface IUnidadDeTrabajo
{
    Task GuardarAsync(CancellationToken ct = default);
}

public sealed record Adjunto(string NombreArchivo, string TipoContenido, byte[] Contenido);

public sealed record MensajeCorreo(
    IReadOnlyList<string> Para,
    string Asunto,
    string CuerpoHtml,
    IReadOnlyList<Adjunto>? Adjuntos = null);

/// <summary>Azure Communication Services Email en producción, SMTP (Mailpit) en desarrollo.</summary>
public interface IEmailSender
{
    Task EnviarAsync(MensajeCorreo mensaje, CancellationToken ct = default);
}

/// <summary>Imágenes de productos. En disco del servidor; intercambiable por Azure Files o Blob.</summary>
public interface IFileStorage
{
    Task<string> GuardarAsync(Stream contenido, string nombreArchivo, string tipoContenido, CancellationToken ct = default);
    Task<Stream?> AbrirAsync(string ruta, CancellationToken ct = default);
    Task EliminarAsync(string ruta, CancellationToken ct = default);
}
