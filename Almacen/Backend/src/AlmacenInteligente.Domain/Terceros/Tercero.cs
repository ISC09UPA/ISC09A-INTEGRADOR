using AlmacenInteligente.Domain.Common;
using AlmacenInteligente.Domain.ValueObjects;

namespace AlmacenInteligente.Domain.Terceros;

/// <summary>Base común de clientes y proveedores. No se borran: se desactivan.</summary>
public abstract class Tercero : AggregateRoot
{
    public string Codigo { get; private set; } = null!;
    public string Nombre { get; private set; } = null!;
    public Correo Correo { get; private set; } = null!;
    public Rfc Rfc { get; private set; } = null!;
    public Direccion Direccion { get; private set; } = null!;
    public bool Activo { get; private set; } = true;
    public Auditoria Creado { get; private set; } = null!;

    protected Tercero() { }

    protected Tercero(string codigo, string nombre, Correo correo, Rfc rfc, Direccion direccion, Auditoria creado)
    {
        Codigo = Guard.Requerido(codigo, "El código", 30).ToUpperInvariant();
        Creado = creado;
        Actualizar(nombre, correo, rfc, direccion);
    }

    public void Actualizar(string nombre, Correo correo, Rfc rfc, Direccion direccion)
    {
        Nombre = Guard.Requerido(nombre, "El nombre", 200);
        Correo = correo;
        Rfc = rfc;
        Direccion = direccion;
    }

    public void Desactivar() => Activo = false;

    public void Activar() => Activo = true;

    internal void AsegurarActivo(string rol)
    {
        if (!Activo)
            throw new DomainException($"El {rol} {Nombre} está inactivo.");
    }
}
