using AlmacenInteligente.Domain.Common;

namespace AlmacenInteligente.Domain.Iot;

public enum EstadoSolicitudInicioSesion
{
    /// <summary>Esperando a que alguien pase su tarjeta y su rostro por el embebido.</summary>
    Pendiente = 1,

    /// <summary>Un acceso permitido la resolvió: el token está listo para entregarse una vez.</summary>
    Completada = 2,

    /// <summary>El acceso que la resolvió fue negado (tarjeta, rostro, usuario desactivado...).</summary>
    Negada = 3,

    /// <summary>Pasaron los segundos de vigencia sin que nadie usara el embebido. Solo se calcula, no se guarda.</summary>
    Expirada = 4,

    /// <summary>El token ya se entregó: no se vuelve a entregar.</summary>
    Consumida = 5,

    /// <summary>La app la canceló.</summary>
    Cancelada = 6,
}

/// <summary>
/// Inicio de sesión de la app móvil con el embebido: la app pide una solicitud, la persona pasa su tarjeta NFC y su rostro
/// por el lector, y el primer acceso que llega mientras la solicitud está vigente la resuelve (permitido o negado).
/// Hay un solo embebido, así que solo puede haber una solicitud pendiente vigente a la vez.
/// La base aplica estas mismas reglas de forma atómica (ver el repositorio); aquí quedan para leerlas y probarlas.
/// </summary>
public sealed class SolicitudInicioSesionIot : Entity
{
    /// <summary>Segundos que la app espera a que la persona use el lector.</summary>
    public const int VigenciaSegundos = 60;

    public DateTimeOffset Creada { get; private set; }
    public DateTimeOffset Expira { get; private set; }
    public EstadoSolicitudInicioSesion Estado { get; private set; }
    public Guid? UsuarioId { get; private set; }
    public string? Motivo { get; private set; }
    public DateTimeOffset? ResueltaEn { get; private set; }

    private SolicitudInicioSesionIot() { }

    /// <summary>
    /// El id es la única credencial de quien sondea la solicitud: es aleatorio (v4) y no secuencial (v7),
    /// para que nadie pueda adivinar el de otra persona y llevarse su token.
    /// </summary>
    public SolicitudInicioSesionIot(DateTimeOffset ahora)
    {
        Id = Guid.NewGuid();
        Creada = ahora;
        Expira = ahora.AddSeconds(VigenciaSegundos);
        Estado = EstadoSolicitudInicioSesion.Pendiente;
    }

    /// <summary>El estado que ve la app: una pendiente pasada de su vigencia se reporta como expirada.</summary>
    public EstadoSolicitudInicioSesion EstadoEn(DateTimeOffset ahora) =>
        Estado == EstadoSolicitudInicioSesion.Pendiente && ahora > Expira ? EstadoSolicitudInicioSesion.Expirada : Estado;

    public bool EstaPendiente(DateTimeOffset ahora) => EstadoEn(ahora) == EstadoSolicitudInicioSesion.Pendiente;

    /// <summary>¿Un acceso registrado en <paramref name="fechaAcceso"/> le corresponde a esta solicitud?</summary>
    public bool AceptaAcceso(DateTimeOffset fechaAcceso) => EstaPendiente(fechaAcceso) && fechaAcceso >= Creada;

    /// <summary>Un acceso permitido la resuelve a favor del usuario reconocido.</summary>
    public void Completar(Guid usuarioId, DateTimeOffset fechaAcceso)
    {
        Guard.Id(usuarioId, "El usuario");
        Resolver(fechaAcceso);
        Estado = EstadoSolicitudInicioSesion.Completada;
        UsuarioId = usuarioId;
        Motivo = null;
    }

    /// <summary>Un acceso negado la cierra con su motivo; la app muestra el motivo y permite reintentar con otra solicitud.</summary>
    public void Negar(string motivo, DateTimeOffset fechaAcceso)
    {
        Resolver(fechaAcceso);
        Estado = EstadoSolicitudInicioSesion.Negada;
        Motivo = Recortar(string.IsNullOrWhiteSpace(motivo) ? "Acceso negado" : motivo.Trim());
    }

    /// <summary>Entrega el usuario para emitir el token una sola vez. Null si no está completada o ya se consumió.</summary>
    public Guid? Consumir()
    {
        if (Estado != EstadoSolicitudInicioSesion.Completada)
            return null;
        Estado = EstadoSolicitudInicioSesion.Consumida;
        return UsuarioId;
    }

    /// <summary>Solo se cancela si sigue pendiente; una ya resuelta se queda como está.</summary>
    public bool Cancelar()
    {
        if (Estado != EstadoSolicitudInicioSesion.Pendiente)
            return false;
        Estado = EstadoSolicitudInicioSesion.Cancelada;
        return true;
    }

    private void Resolver(DateTimeOffset fechaAcceso)
    {
        if (!AceptaAcceso(fechaAcceso))
            throw new DomainException("La solicitud de inicio de sesión ya no está pendiente.");
        ResueltaEn = fechaAcceso;
    }

    private static string Recortar(string texto) => texto.Length > 300 ? texto[..300] : texto;
}
