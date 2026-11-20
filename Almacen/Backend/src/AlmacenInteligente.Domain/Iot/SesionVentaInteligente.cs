using AlmacenInteligente.Domain.Common;

namespace AlmacenInteligente.Domain.Iot;

/// <summary>
/// Candado del modo inteligente de venta: hay un solo lector NFC, así que solo un usuario a la vez recibe sus escaneos.
/// Es una fila única (<see cref="IdUnico"/>). El candado vence si el dueño deja de mandar latidos durante el tiempo límite.
/// La base aplica estas mismas reglas de forma atómica al tomarlo (ver el repositorio); aquí quedan para leerlas y probarlas.
/// </summary>
public sealed class SesionVentaInteligente
{
    public const int IdUnico = 1;

    public int Id { get; private set; } = IdUnico;

    /// <summary>Cambia cada vez que alguien toma el candado libre o vencido: los escaneos se ligan a esta sesión.</summary>
    public Guid? SesionId { get; private set; }
    public Guid? UsuarioId { get; private set; }
    public Guid? AlmacenId { get; private set; }
    public DateTimeOffset? Inicio { get; private set; }
    public DateTimeOffset? UltimoLatido { get; private set; }

    public bool EstaVigente(DateTimeOffset ahora, TimeSpan limite) =>
        UsuarioId is not null && UltimoLatido is { } latido && ahora - latido <= limite;

    /// <summary>Se puede tomar si está libre, vencido o ya es de ese usuario.</summary>
    public bool PuedeTomar(Guid usuarioId, DateTimeOffset ahora, TimeSpan limite) =>
        !EstaVigente(ahora, limite) || UsuarioId == usuarioId;

    public void Tomar(Guid usuarioId, Guid almacenId, DateTimeOffset ahora, TimeSpan limite)
    {
        Guard.Id(usuarioId, "El usuario");
        Guard.Id(almacenId, "El almacén");
        if (!PuedeTomar(usuarioId, ahora, limite))
            throw new DomainException("El modo inteligente lo está usando otro usuario.");

        // Retomarlo uno mismo mientras sigue vigente conserva la sesión (y sus escaneos pendientes).
        if (!(UsuarioId == usuarioId && EstaVigente(ahora, limite)))
        {
            SesionId = Guid.CreateVersion7();
            Inicio = ahora;
        }
        UsuarioId = usuarioId;
        AlmacenId = almacenId;
        UltimoLatido = ahora;
    }

    /// <summary>Solo el dueño renueva el candado. Devuelve false si ya no es suyo.</summary>
    public bool Latido(Guid usuarioId, DateTimeOffset ahora)
    {
        if (UsuarioId != usuarioId)
            return false;
        UltimoLatido = ahora;
        return true;
    }

    public void Liberar(Guid usuarioId)
    {
        if (UsuarioId != usuarioId)
            return;
        UsuarioId = null;
        AlmacenId = null;
        SesionId = null;
        Inicio = null;
        UltimoLatido = null;
    }
}
