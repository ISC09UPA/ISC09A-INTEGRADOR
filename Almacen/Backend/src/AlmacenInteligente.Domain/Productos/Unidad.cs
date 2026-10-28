using AlmacenInteligente.Domain.Common;

namespace AlmacenInteligente.Domain.Productos;

/// <summary>Catálogo de unidades de medida (pieza, kg, litro, caja...).</summary>
public sealed class Unidad : Entity
{
    public string Clave { get; private set; } = null!;
    public string Nombre { get; private set; } = null!;

    /// <summary>Si es false, las cantidades deben ser enteras (piezas, cajas).</summary>
    public bool PermiteFracciones { get; private set; }

    private Unidad() { }

    public Unidad(string clave, string nombre, bool permiteFracciones)
    {
        Clave = Guard.Requerido(clave, "La clave de la unidad", 10).ToUpperInvariant();
        Nombre = Guard.Requerido(nombre, "El nombre de la unidad", 60);
        PermiteFracciones = permiteFracciones;
    }
}
