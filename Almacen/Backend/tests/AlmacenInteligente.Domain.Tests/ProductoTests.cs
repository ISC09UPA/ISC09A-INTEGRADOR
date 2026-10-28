using AlmacenInteligente.Domain.Common;
using AlmacenInteligente.Domain.Productos;
using AlmacenInteligente.Domain.Recepciones;
using AlmacenInteligente.Domain.ValueObjects;

namespace AlmacenInteligente.Domain.Tests;

public class ProductoTests
{
    private static readonly Guid Gerente = Guid.NewGuid();

    private static Producto Pendiente() =>
        Producto.CrearPendiente("cab-01", "Cable", Fabrica.Pieza(), Gerente, Fabrica.Registro());

    [Fact]
    public void Alta_por_encargado_queda_pendiente_y_avisa_al_gerente_elegido()
    {
        var producto = Pendiente();

        Assert.Equal(EstadoAprobacion.Pendiente, producto.Estado);
        Assert.Equal("CAB-01", producto.Referencia);
        var evento = Assert.IsType<ProductoPendienteDeAprobacion>(Assert.Single(producto.Eventos));
        Assert.Equal(Gerente, evento.GerenteId);
    }

    [Fact]
    public void Solo_el_gerente_elegido_puede_aprobar()
    {
        var producto = Pendiente();

        Assert.Throws<DomainException>(() => producto.Aprobar(Fabrica.Registro(Guid.NewGuid())));

        producto.Aprobar(Fabrica.Registro(Gerente));
        Assert.True(producto.EstaDisponible);
        Assert.Throws<DomainException>(() => producto.Rechazar(Fabrica.Registro(Gerente), "tarde"));
    }

    [Fact]
    public void Rechazo_exige_motivo()
    {
        var producto = Pendiente();
        Assert.Throws<DomainException>(() => producto.Rechazar(Fabrica.Registro(Gerente), " "));
        producto.Rechazar(Fabrica.Registro(Gerente), "Duplicado");
        Assert.Equal(EstadoAprobacion.Rechazado, producto.Estado);
    }

    [Fact]
    public void Producto_pendiente_no_se_puede_recibir()
    {
        var recepcion = new Recepcion(Fabrica.Proveedor(), Fabrica.Almacen(), Fabrica.Hoy, Fabrica.Registro());
        Assert.Throws<DomainException>(() => recepcion.AgregarLinea(Pendiente(), 1, 10, TasaIva.General));
    }

    [Fact]
    public void Unidad_entera_rechaza_fracciones()
    {
        var recepcion = new Recepcion(Fabrica.Proveedor(), Fabrica.Almacen(), Fabrica.Hoy, Fabrica.Registro());
        Assert.Throws<DomainException>(() => recepcion.AgregarLinea(Fabrica.Producto(), 1.5m, 10, TasaIva.General));
        recepcion.AgregarLinea(Fabrica.Producto("AZU-1", Fabrica.Kilo()), 1.5m, 10, TasaIva.General);
    }
}
