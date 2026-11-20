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

/// <summary>Un producto que pasó por el lector NFC mientras alguien tenía el modo inteligente de venta.</summary>
public sealed class EscaneoIot : Entity
{
    public Guid SesionId { get; private set; }
    public Guid ProductoId { get; private set; }
    public DateTimeOffset Fecha { get; private set; }
    public bool Procesado { get; private set; }

    private EscaneoIot() { }

    public EscaneoIot(Guid sesionId, Guid productoId, DateTimeOffset fecha)
    {
        SesionId = sesionId;
        ProductoId = productoId;
        Fecha = fecha;
    }

    public void MarcarProcesado() => Procesado = true;
}
