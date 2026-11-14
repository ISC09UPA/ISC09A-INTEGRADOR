using AlmacenInteligente.Domain.Common;

namespace AlmacenInteligente.Domain.Productos;

public enum EstadoAprobacion
{
    Pendiente = 1,
    Aprobado = 2,
    Rechazado = 3,
}

/// <summary>
/// Producto del catálogo. Si lo da de alta el encargado de recepciones queda Pendiente y lo aprueba el gerente
/// que el encargado eligió; si lo crea un superadmin o gerente queda Aprobado. Solo el alta requiere aprobación.
/// </summary>
public sealed class Producto : AggregateRoot
{
    public string Referencia { get; private set; } = null!;
    public string Descripcion { get; private set; } = null!;
    public Guid UnidadId { get; private set; }
    public bool PermiteFracciones { get; private set; }
    public string? ImagenRuta { get; private set; }
    public bool Activo { get; private set; } = true;

    public EstadoAprobacion Estado { get; private set; }
    public Guid? GerenteAprobadorId { get; private set; }
    public Auditoria Creado { get; private set; } = null!;
    public Auditoria? Resolucion { get; private set; }
    public string? MotivoRechazo { get; private set; }

    public bool EstaDisponible => Activo && Estado == EstadoAprobacion.Aprobado;

    private Producto() { }

    private Producto(string referencia, string descripcion, Unidad unidad, Auditoria creado)
    {
        Referencia = Guard.Requerido(referencia, "La referencia", 50).ToUpperInvariant();
        Creado = creado;
        Actualizar(descripcion, unidad);
    }

    /// <summary>Alta por superadmin o gerente: no requiere aprobación.</summary>
    public static Producto CrearAprobado(string referencia, string descripcion, Unidad unidad, Auditoria creado)
    {
        var producto = new Producto(referencia, descripcion, unidad, creado)
        {
            Estado = EstadoAprobacion.Aprobado,
            Resolucion = creado,
        };
        return producto;
    }

    /// <summary>Alta por el encargado de recepciones: queda pendiente del gerente elegido.</summary>
    public static Producto CrearPendiente(string referencia, string descripcion, Unidad unidad, Guid gerenteAprobadorId, Auditoria creado)
    {
        var producto = new Producto(referencia, descripcion, unidad, creado)
        {
            Estado = EstadoAprobacion.Pendiente,
            GerenteAprobadorId = Guard.Id(gerenteAprobadorId, "El gerente que aprueba"),
        };
        producto.Emitir(new ProductoPendienteDeAprobacion(producto.Id, gerenteAprobadorId, creado.UsuarioId, creado.Fecha));
        return producto;
    }

    public void Actualizar(string descripcion, Unidad unidad)
    {
        Descripcion = Guard.Requerido(descripcion, "La descripción", 500);
        UnidadId = unidad.Id;
        PermiteFracciones = unidad.PermiteFracciones;
    }

    public void AsignarImagen(string? ruta) => ImagenRuta = Guard.Opcional(ruta, "La ruta de la imagen", 400);

    public void Aprobar(Auditoria aprobacion)
    {
        AsegurarResolvible(aprobacion.UsuarioId);
        Estado = EstadoAprobacion.Aprobado;
        Resolucion = aprobacion;
        Emitir(new ProductoAprobado(Id, Creado.UsuarioId, aprobacion.UsuarioId, aprobacion.Fecha));
    }

    public void Rechazar(Auditoria rechazo, string motivo)
    {
        AsegurarResolvible(rechazo.UsuarioId);
        MotivoRechazo = Guard.Requerido(motivo, "El motivo del rechazo", 500);
        Estado = EstadoAprobacion.Rechazado;
        Resolucion = rechazo;
        Emitir(new ProductoRechazado(Id, Creado.UsuarioId, rechazo.UsuarioId, MotivoRechazo, rechazo.Fecha));
    }

    public void Desactivar() => Activo = false;

    public void Activar() => Activo = true;

    /// <summary>Valida que el producto se pueda usar en recepciones, ventas y traspasos, y que la cantidad respete la unidad.</summary>
    internal void AsegurarUsable(decimal cantidad)
    {
        if (!EstaDisponible)
            throw new DomainException($"El producto {Referencia} no está aprobado o está inactivo.");
        if (!PermiteFracciones && decimal.Truncate(cantidad) != cantidad)
            throw new DomainException($"El producto {Referencia} solo admite cantidades enteras.");
    }

    private void AsegurarResolvible(Guid usuarioId)
    {
        if (Estado != EstadoAprobacion.Pendiente)
            throw new DomainException($"El producto {Referencia} ya fue {Estado.ToString().ToLowerInvariant()}.");
        if (usuarioId != GerenteAprobadorId)
            throw new DomainException("Solo el gerente al que se envió la solicitud puede resolverla.");
    }
}

public sealed record ProductoPendienteDeAprobacion(Guid ProductoId, Guid GerenteId, Guid SolicitanteId, DateTimeOffset OcurrioEn) : IDomainEvent;

public sealed record ProductoAprobado(Guid ProductoId, Guid SolicitanteId, Guid GerenteId, DateTimeOffset OcurrioEn) : IDomainEvent;

public sealed record ProductoRechazado(Guid ProductoId, Guid SolicitanteId, Guid GerenteId, string Motivo, DateTimeOffset OcurrioEn) : IDomainEvent;
