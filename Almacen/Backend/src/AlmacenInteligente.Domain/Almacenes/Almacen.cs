using AlmacenInteligente.Domain.Common;

namespace AlmacenInteligente.Domain.Almacenes;

public sealed class Almacen : AggregateRoot
{
    public string Nombre { get; private set; } = null!;
    public string Ubicacion { get; private set; } = null!;
    public bool Activo { get; private set; } = true;
    public Auditoria Creado { get; private set; } = null!;

    private Almacen() { }

    public Almacen(string nombre, string ubicacion, Auditoria creado)
    {
        Creado = creado;
        Actualizar(nombre, ubicacion);
    }

    public void Actualizar(string nombre, string ubicacion)
    {
        Nombre = Guard.Requerido(nombre, "El nombre del almacén", 120);
        Ubicacion = Guard.Requerido(ubicacion, "La ubicación", 300);
    }

    public void Desactivar() => Activo = false;

    public void Activar() => Activo = true;

    internal void AsegurarActivo()
    {
        if (!Activo)
            throw new DomainException($"El almacén {Nombre} está inactivo.");
    }
}
