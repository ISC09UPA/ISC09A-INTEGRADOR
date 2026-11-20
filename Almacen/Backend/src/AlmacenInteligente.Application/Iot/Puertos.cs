using AlmacenInteligente.Application.Terceros;
using AlmacenInteligente.Domain.Common;
using AlmacenInteligente.Domain.Iot;

namespace AlmacenInteligente.Application.Iot;

/// <summary>Reglas del embebido que viven en la aplicación (sección "Iot" de la configuración).</summary>
public sealed class OpcionesIot
{
    /// <summary>Probabilidad mínima (0 a 1) que debe dar el modelo para el dueño de la tarjeta.</summary>
    public double UmbralConfianza { get; init; } = 0.8;

    /// <summary>Minutos sin latido tras los que vence el candado del modo inteligente de venta.</summary>
    public double TimeoutSesionMinutos { get; init; } = 2;

    public TimeSpan TimeoutSesion => TimeSpan.FromMinutes(TimeoutSesionMinutos);
}

/// <summary>
/// Puerto hacia Azure Custom Vision: un proyecto de clasificación multiclase con una etiqueta por usuario
/// (el nombre de la etiqueta es el Id del usuario).
/// </summary>
public interface IReconocimientoFacial
{
    /// <summary>False si falta la sección CustomVision: las fotos se guardan y se suben al entrenar.</summary>
    bool Configurado { get; }

    /// <summary>Sube la imagen con la etiqueta (la crea si no existe) y devuelve el id de la imagen.</summary>
    Task<string> SubirImagenAsync(string etiqueta, byte[] imagen, CancellationToken ct = default);

    Task EliminarImagenesAsync(IReadOnlyCollection<string> imagenIds, CancellationToken ct = default);

    /// <summary>Borra la etiqueta si existe (cuando el usuario ya no tiene fotos).</summary>
    Task EliminarEtiquetaAsync(string etiqueta, CancellationToken ct = default);

    /// <summary>Imágenes por etiqueta en el proyecto.</summary>
    Task<IReadOnlyDictionary<string, int>> ContarImagenesPorEtiquetaAsync(CancellationToken ct = default);

    /// <summary>Entrena, espera a que termine y publica la iteración (despublicando la anterior). Devuelve su nombre.</summary>
    Task<string> EntrenarYPublicarAsync(CancellationToken ct = default);

    /// <summary>
    /// Clasifica la imagen con la iteración publicada. Lanza <see cref="ImagenInvalidaException"/> si Custom Vision la
    /// rechaza y <see cref="ReconocimientoNoDisponibleException"/> si no hay modelo publicado o falta configuración.
    /// </summary>
    Task<IReadOnlyList<PrediccionRostro>> ClasificarAsync(byte[] imagen, CancellationToken ct = default);
}

/// <summary>La imagen no sirve (formato, tamaño, vacía). No tiene caso reintentar.</summary>
public sealed class ImagenInvalidaException(string mensaje) : DomainException(mensaje);

/// <summary>Custom Vision no está configurado o no hay iteración publicada. No tiene caso reintentar.</summary>
public sealed class ReconocimientoNoDisponibleException(string mensaje) : DomainException(mensaje);

public interface IFotosRostroRepositorio
{
    Task<FotoRostro?> ObtenerAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<FotoRostro>> ListarAsync(Guid usuarioId, CancellationToken ct = default);
    Task<IReadOnlyList<FotoRostro>> ListarTodasAsync(CancellationToken ct = default);
    void Agregar(FotoRostro foto);
    void Eliminar(FotoRostro foto);
}

public interface IAccesosIotRepositorio
{
    void Agregar(AccesoIot acceso);
    Task<Pagina<AccesoIotDto>> ListarAsync(FiltroAccesosIot filtro, CancellationToken ct = default);
}

/// <summary>
/// El candado del modo inteligente de venta. Las operaciones son atómicas en la base (una sola sentencia),
/// así dos agentes que activan el modo al mismo tiempo no pueden quedarse ambos con el lector.
/// </summary>
public interface ICandadoVentaInteligente
{
    Task<SesionVentaInteligente?> ObtenerAsync(CancellationToken ct = default);

    /// <summary>Toma el candado si está libre, vencido (latido anterior a <paramref name="venceAntesDe"/>) o ya es del usuario.</summary>
    Task<bool> IntentarTomarAsync(Guid usuarioId, Guid almacenId, DateTimeOffset ahora, DateTimeOffset venceAntesDe, CancellationToken ct = default);

    /// <summary>Renueva el latido; false si el candado ya no es del usuario.</summary>
    Task<bool> LatidoAsync(Guid usuarioId, DateTimeOffset ahora, CancellationToken ct = default);

    Task LiberarAsync(Guid usuarioId, CancellationToken ct = default);

    /// <summary>Registra el escaneo en la sesión vigente. False si nadie tiene el candado vigente.</summary>
    Task<bool> RegistrarEscaneoAsync(Guid productoId, DateTimeOffset ahora, DateTimeOffset venceAntesDe, CancellationToken ct = default);

    /// <summary>Devuelve los escaneos no procesados de la sesión del usuario y los marca procesados.</summary>
    Task<IReadOnlyList<EscaneoIot>> TomarEscaneosPendientesAsync(Guid usuarioId, CancellationToken ct = default);
}

public sealed record FiltroAccesosIot(bool? Permitido = null, DateOnly? Desde = null, DateOnly? Hasta = null, int NumeroPagina = 1, int Tamano = 50);

public sealed record AccesoIotDto(
    Guid Id, DateTimeOffset Fecha, string? Nfc, Guid? UsuarioId, string? Usuario, bool Permitido, double Confianza, string Motivo);

public sealed record FotoRostroDto(Guid Id, string Ruta, DateTimeOffset SubidaEn, bool EnCustomVision, bool Entrenada);

public sealed record ResultadoEntrenamientoDto(string Iteracion, int Usuarios, int Fotos);

/// <summary>Lo que el embebido recibe en la cola de respuestas.</summary>
public sealed record RespuestaAccesoIot(bool Permitido, Guid? UsuarioId, string? Nombre, double Confianza, string Motivo);

public sealed record SesionVentaDto(
    Guid SesionId, Guid UsuarioId, string Usuario, Guid AlmacenId, string Almacen,
    DateTimeOffset Inicio, DateTimeOffset UltimoLatido, bool EsMia);

public sealed record EscaneoDto(Guid Id, DateTimeOffset Fecha, Consultas.ProductoOpcionDto Producto);
