using AlmacenInteligente.Domain.Almacenes;
using AlmacenInteligente.Domain.Common;
using AlmacenInteligente.Domain.Productos;
using AlmacenInteligente.Domain.Terceros;
using AlmacenInteligente.Domain.ValueObjects;

namespace AlmacenInteligente.Domain.Tests;

internal static class Fabrica
{
    public static readonly DateTimeOffset Ahora = new(2026, 10, 22, 10, 0, 0, TimeSpan.FromHours(-6));
    public static readonly DateOnly Hoy = DateOnly.FromDateTime(Ahora.Date);
    public static readonly Guid Usuario = Guid.NewGuid();

    public static Auditoria Registro(Guid? usuario = null) => new(usuario ?? Usuario, Ahora);

    public static Direccion Direccion() =>
        new("Av. Reforma", "100", null, "Juárez", "06600", "Cuauhtémoc", "Ciudad de México");

    public static Cliente Cliente() =>
        new("CLI-1", "Comercial del Norte", Correo.Crear("compras@norte.mx"), Rfc.Crear("CNO010203AB1"), Direccion(), Registro());

    public static Proveedor Proveedor() =>
        new("PRV-1", "Distribuidora Sur", Correo.Crear("ventas@sur.mx"), Rfc.Crear("DSU990101XY2"), Direccion(), Registro());

    public static Unidad Pieza() => new("PZA", "Pieza", permiteFracciones: false);

    public static Unidad Kilo() => new("KG", "Kilogramo", permiteFracciones: true);

    public static Producto Producto(string referencia = "TOR-001", Unidad? unidad = null) =>
        Domain.Productos.Producto.CrearAprobado(referencia, "Tornillo", unidad ?? Pieza(), Registro());

    public static Almacen Almacen(string nombre = "Central") => new(nombre, "Monterrey", Registro());
}
