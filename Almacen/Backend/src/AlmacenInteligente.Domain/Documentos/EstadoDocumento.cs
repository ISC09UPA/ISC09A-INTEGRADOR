using AlmacenInteligente.Domain.Common;

namespace AlmacenInteligente.Domain.Documentos;

public enum EstadoDocumento
{
    Borrador = 1,
    Confirmado = 2,
    Cancelado = 3,
}

/// <summary>Ciclo de vida común de recepciones, ventas y traspasos: Borrador → Confirmado → Cancelado.</summary>
public abstract class Documento : AggregateRoot
{
    /// <summary>Número consecutivo legible, por tipo de documento. Lo asigna la base de datos al guardar.</summary>
    public long Folio { get; private set; }

    public EstadoDocumento Estado { get; private set; } = EstadoDocumento.Borrador;
    public Auditoria Creado { get; private set; } = null!;
    public Auditoria? Confirmado { get; private set; }
    public Auditoria? Cancelado { get; private set; }
    public string? MotivoCancelacion { get; private set; }

    protected Documento() { }

    protected Documento(Auditoria creado) => Creado = creado;

    protected abstract int NumeroDeLineas { get; }

    protected abstract string Nombre { get; }

    protected abstract bool NombreFemenino { get; }

    private string Articulo => NombreFemenino ? "La" : "El";

    protected void AsegurarBorrador()
    {
        if (Estado != EstadoDocumento.Borrador)
            throw new DomainException($"{Articulo} {Nombre} ya no se puede modificar.");
    }

    internal void MarcarConfirmado(Auditoria confirmacion)
    {
        AsegurarBorrador();
        if (NumeroDeLineas == 0)
            throw new DomainException($"{Articulo} {Nombre} debe tener al menos una línea.");
        Estado = EstadoDocumento.Confirmado;
        Confirmado = confirmacion;
    }

    internal void MarcarCancelado(Auditoria cancelacion, string motivo)
    {
        if (Estado != EstadoDocumento.Confirmado)
            throw new DomainException(NombreFemenino
                ? $"Solo se puede cancelar una {Nombre} confirmada."
                : $"Solo se puede cancelar un {Nombre} confirmado.");
        MotivoCancelacion = Guard.Requerido(motivo, "El motivo de cancelación", 500);
        Estado = EstadoDocumento.Cancelado;
        Cancelado = cancelacion;
    }
}
