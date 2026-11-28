using AlmacenInteligente.Domain.Common;

namespace AlmacenInteligente.Domain.Iot;

public enum EstadoReconocimiento
{
    SinFotos = 1,
    FaltanFotos = 2,
    PendienteDeEntrenar = 3,
    Entrenado = 4,
}

/// <summary>Foto del rostro de un usuario para entrenar el modelo de Custom Vision (una etiqueta por usuario).</summary>
public sealed class FotoRostro : Entity
{
    /// <summary>Custom Vision exige al menos 5 imágenes por etiqueta para entrenar.</summary>
    public const int MinimoPorUsuario = 5;

    public Guid UsuarioId { get; private set; }
    public string Ruta { get; private set; } = null!;

    /// <summary>Id de la imagen en Custom Vision; null mientras no se ha subido.</summary>
    public string? ImagenRemotaId { get; private set; }
    public DateTimeOffset SubidaEn { get; private set; }

    /// <summary>Cuándo entró en un modelo publicado; null si el modelo actual no la incluye.</summary>
    public DateTimeOffset? EntrenadaEn { get; private set; }

    private FotoRostro() { }

    public FotoRostro(Guid usuarioId, string ruta, DateTimeOffset subidaEn)
    {
        UsuarioId = Guard.Id(usuarioId, "El usuario");
        Ruta = Guard.Requerido(ruta, "La ruta de la foto", 400);
        SubidaEn = subidaEn;
    }

    public void MarcarSubida(string imagenRemotaId) => ImagenRemotaId = Guard.Requerido(imagenRemotaId, "El id de la imagen", 100);

    public void MarcarEntrenada(DateTimeOffset fecha) => EntrenadaEn = fecha;

    public void ReiniciarEntrenamiento() => EntrenadaEn = null;

    public static EstadoReconocimiento Estado(int fotos, int entrenadas) =>
        fotos == 0 ? EstadoReconocimiento.SinFotos
        : fotos < MinimoPorUsuario ? EstadoReconocimiento.FaltanFotos
        : entrenadas < fotos ? EstadoReconocimiento.PendienteDeEntrenar
        : EstadoReconocimiento.Entrenado;
}

/// <summary>Bitácora de cada intento de acceso con tarjeta + rostro.</summary>
public sealed class AccesoIot : Entity
{
    public DateTimeOffset Fecha { get; private set; }
    public string? Nfc { get; private set; }
    public Guid? UsuarioId { get; private set; }
    public bool Permitido { get; private set; }
    public double Confianza { get; private set; }
    public string Motivo { get; private set; } = null!;

    private AccesoIot() { }

    public AccesoIot(DateTimeOffset fecha, string? nfc, Guid? usuarioId, bool permitido, double confianza, string motivo)
    {
        Fecha = fecha;
        Nfc = nfc is { Length: > CodigoNfc.LongitudMaxima } ? nfc[..CodigoNfc.LongitudMaxima] : nfc;
        UsuarioId = usuarioId;
        Permitido = permitido;
        Confianza = Math.Clamp(confianza, 0, 1);
        Motivo = motivo.Length > 300 ? motivo[..300] : motivo;
    }
}

/// <summary>
/// Una pieza que pasó por el lector NFC mientras alguien tenía el modo inteligente de venta. El embebido manda el UID
/// de fábrica de la etiqueta (cada etiqueta es una pieza física) y la familia (el código NFC grabado en el producto del catálogo).
/// Los escaneos rechazados (pieza ya vendida, familia desconocida…) también se guardan con su motivo, para avisarle al agente.
/// </summary>
public sealed class EscaneoIot : Entity
{
    public const int LongitudMaximaFamilia = 50;
    public const int LongitudMaximaRechazo = 200;

    public Guid SesionId { get; private set; }
    public string Uid { get; private set; } = null!;
    public string Familia { get; private set; } = null!;

    /// <summary>Producto de la familia; null si la familia no existe en el catálogo.</summary>
    public Guid? ProductoId { get; private set; }

    /// <summary>Por qué no se agregó a la venta; null si se aceptó.</summary>
    public string? Rechazo { get; private set; }
    public DateTimeOffset Fecha { get; private set; }
    public bool Procesado { get; private set; }

    /// <summary>Rechazo por duplicado (la pieza o el paquete ya se leyó en la sesión): es una alerta, no un error.</summary>
    public bool Duplicado { get; private set; }

    public bool Aceptado => Rechazo is null;

    private EscaneoIot() { }

    public EscaneoIot(Guid sesionId, string uid, string familia, Guid? productoId, DateTimeOffset fecha, string? rechazo = null, bool duplicado = false)
    {
        Duplicado = duplicado && rechazo is not null;
        SesionId = sesionId;
        Uid = Guard.Requerido(uid, "El UID de la pieza", CodigoNfc.LongitudMaxima);
        Familia = Recortar(familia, LongitudMaximaFamilia);
        ProductoId = productoId;
        Fecha = fecha;
        Rechazo = rechazo is null ? null : Recortar(rechazo, LongitudMaximaRechazo);
    }

    public void MarcarProcesado() => Procesado = true;

    private static string Recortar(string valor, int maximo) => valor.Length > maximo ? valor[..maximo] : valor;
}
