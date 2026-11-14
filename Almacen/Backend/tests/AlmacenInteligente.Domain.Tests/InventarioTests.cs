using AlmacenInteligente.Domain.Common;
using AlmacenInteligente.Domain.Documentos;
using AlmacenInteligente.Domain.Finanzas;
using AlmacenInteligente.Domain.Inventario;
using AlmacenInteligente.Domain.Productos;
using AlmacenInteligente.Domain.Recepciones;
using AlmacenInteligente.Domain.Ventas;
using AlmacenInteligente.Domain.ValueObjects;

namespace AlmacenInteligente.Domain.Tests;

public class InventarioTests
{
    private readonly Almacenes.Almacen _central = Fabrica.Almacen("Central");
    private readonly Almacenes.Almacen _norte = Fabrica.Almacen("Norte");
    private readonly Producto _tornillo = Fabrica.Producto();

    private Recepcion Recibir(LoteExistencias lote, decimal cantidad, decimal costo)
    {
        var recepcion = new Recepcion(Fabrica.Proveedor(), _central, Fabrica.Hoy, Fabrica.Registro());
        recepcion.AgregarLinea(_tornillo, cantidad, costo, TasaIva.General);
        ServicioInventario.ConfirmarRecepcion(recepcion, lote, Fabrica.Registro());
        return recepcion;
    }

    private Venta NuevaVenta(decimal cantidad, decimal precio = 20m)
    {
        var venta = new Venta(Fabrica.Cliente(), Fabrica.Hoy, Fabrica.Registro());
        venta.AgregarLinea(_tornillo, _central.Id, cantidad, precio, TasaIva.General);
        return venta;
    }

    [Fact]
    public void Recepcion_crea_existencia_kardex_y_egreso()
    {
        var lote = new LoteExistencias([]);
        var recepcion = new Recepcion(Fabrica.Proveedor(), _central, Fabrica.Hoy, Fabrica.Registro());
        recepcion.AgregarLinea(_tornillo, 10, 5m, TasaIva.General);

        var resultado = ServicioInventario.ConfirmarRecepcion(recepcion, lote, Fabrica.Registro());

        var existencia = Assert.Single(lote.Nuevas);
        Assert.Equal(10, existencia.Cantidad);
        Assert.Equal(5m, existencia.CostoPromedio);
        var kardex = Assert.Single(resultado.Kardex);
        Assert.Equal(TipoMovimientoInventario.Recepcion, kardex.Tipo);
        Assert.Equal(10, kardex.SaldoCantidad);
        Assert.Equal(NaturalezaFinanciera.Egreso, resultado.Financiero!.Naturaleza);
        Assert.Equal(58m, resultado.Financiero.Importe.Total.Monto);
        Assert.Equal(EstadoDocumento.Confirmado, recepcion.Estado);
        Assert.IsType<RecepcionConfirmada>(Assert.Single(recepcion.Eventos));
    }

    [Fact]
    public void Costo_promedio_ponderado()
    {
        var lote = new LoteExistencias([]);
        Recibir(lote, 10, 5m);
        Recibir(lote, 30, 9m);

        // (10×5 + 30×9) / 40 = 8
        Assert.Equal(8m, lote.Todas.Single().CostoPromedio);
    }

    [Fact]
    public void Venta_descuenta_al_costo_promedio_y_registra_ingreso()
    {
        var lote = new LoteExistencias([]);
        Recibir(lote, 10, 5m);
        var venta = NuevaVenta(4, precio: 20m);

        var resultado = ServicioInventario.ConfirmarVenta(venta, lote, Fabrica.Registro());

        Assert.Equal(6, lote.Todas.Single().Cantidad);
        Assert.Equal(5m, venta.Lineas[0].CostoUnitario);
        Assert.Equal(-4, resultado.Kardex.Single().Cantidad);
        Assert.Equal(NaturalezaFinanciera.Ingreso, resultado.Financiero!.Naturaleza);
        Assert.Equal(80m, resultado.Financiero.Importe.Subtotal.Monto);
        Assert.Equal(12.8m, resultado.Financiero.Importe.Iva.Monto);
    }

    [Fact]
    public void Venta_nunca_deja_existencia_negativa_ni_cambios_parciales()
    {
        var lote = new LoteExistencias([]);
        Recibir(lote, 5, 5m);
        var venta = new Venta(Fabrica.Cliente(), Fabrica.Hoy, Fabrica.Registro());
        venta.AgregarLinea(_tornillo, _central.Id, 3, 20, TasaIva.General);
        venta.AgregarLinea(_tornillo, _central.Id, 3, 20, TasaIva.General);

        Assert.Throws<ExistenciaInsuficienteException>(() => ServicioInventario.ConfirmarVenta(venta, lote, Fabrica.Registro()));

        Assert.Equal(5, lote.Todas.Single().Cantidad);
        Assert.Equal(EstadoDocumento.Borrador, venta.Estado);
    }

    [Fact]
    public void Venta_en_almacen_sin_existencia_falla()
    {
        var venta = NuevaVenta(1);
        Assert.Throws<DomainException>(() => ServicioInventario.ConfirmarVenta(venta, new LoteExistencias([]), Fabrica.Registro()));
    }

    [Fact]
    public void Cancelar_venta_repone_existencia_y_revierte_ingreso()
    {
        var lote = new LoteExistencias([]);
        Recibir(lote, 10, 5m);
        var venta = NuevaVenta(4);
        ServicioInventario.ConfirmarVenta(venta, lote, Fabrica.Registro());

        var resultado = ServicioInventario.CancelarVenta(venta, lote, Fabrica.Registro(), "Cliente desistió");

        Assert.Equal(10, lote.Todas.Single().Cantidad);
        Assert.Equal(5m, lote.Todas.Single().CostoPromedio);
        Assert.Equal(EstadoDocumento.Cancelado, venta.Estado);
        Assert.True(resultado.Financiero!.EsReverso);
        Assert.Equal(-92.8m, resultado.Financiero.Importe.Total.Monto);
        Assert.Throws<DomainException>(() => ServicioInventario.CancelarVenta(venta, lote, Fabrica.Registro(), "otra vez"));
    }

    [Fact]
    public void Cancelar_recepcion_quita_su_aporte_al_costo()
    {
        var lote = new LoteExistencias([]);
        Recibir(lote, 10, 5m);
        var segunda = Recibir(lote, 30, 9m);

        ServicioInventario.CancelarRecepcion(segunda, lote, Fabrica.Registro(), "Error de captura");

        var existencia = lote.Todas.Single();
        Assert.Equal(10, existencia.Cantidad);
        Assert.Equal(5m, existencia.CostoPromedio);
    }

    [Fact]
    public void No_se_cancela_recepcion_si_la_mercancia_ya_salio()
    {
        var lote = new LoteExistencias([]);
        var recepcion = Recibir(lote, 10, 5m);
        ServicioInventario.ConfirmarVenta(NuevaVenta(8), lote, Fabrica.Registro());

        Assert.Throws<ExistenciaInsuficienteException>(() =>
            ServicioInventario.CancelarRecepcion(recepcion, lote, Fabrica.Registro(), "Devolución"));
        Assert.Equal(EstadoDocumento.Confirmado, recepcion.Estado);
    }

    [Fact]
    public void Traspaso_mueve_al_costo_de_origen_sin_movimiento_financiero()
    {
        var lote = new LoteExistencias([]);
        Recibir(lote, 10, 5m);
        var traspaso = new Traspaso(_central, _norte, Fabrica.Hoy, Fabrica.Registro());
        traspaso.AgregarLinea(_tornillo, 4);

        var resultado = ServicioInventario.ConfirmarTraspaso(traspaso, lote, Fabrica.Registro());

        Assert.Null(resultado.Financiero);
        Assert.Equal(2, resultado.Kardex.Count);
        Assert.Equal(6, lote.ParaSalida(_central.Id, _tornillo.Id).Cantidad);
        var destino = lote.ParaSalida(_norte.Id, _tornillo.Id);
        Assert.Equal(4, destino.Cantidad);
        Assert.Equal(5m, destino.CostoPromedio);
    }

    [Fact]
    public void Traspaso_al_mismo_almacen_no_se_permite() =>
        Assert.Throws<DomainException>(() => new Traspaso(_central, _central, Fabrica.Hoy, Fabrica.Registro()));

    [Fact]
    public void Alerta_de_bajo_stock_solo_al_cruzar_el_minimo_y_se_rearma()
    {
        var lote = new LoteExistencias([]);
        Recibir(lote, 10, 5m);
        var existencia = lote.Todas.Single();
        existencia.DefinirStockMinimo(5, Fabrica.Ahora);
        existencia.LimpiarEventos();

        ServicioInventario.ConfirmarVenta(NuevaVenta(4), lote, Fabrica.Registro()); // 6: arriba del mínimo
        Assert.Empty(existencia.Eventos);

        ServicioInventario.ConfirmarVenta(NuevaVenta(2), lote, Fabrica.Registro()); // 4: cruza
        Assert.IsType<StockBajoMinimo>(Assert.Single(existencia.Eventos));

        ServicioInventario.ConfirmarVenta(NuevaVenta(1), lote, Fabrica.Registro()); // 3: sigue abajo, no repite
        Assert.Single(existencia.Eventos);

        Recibir(lote, 10, 5m);                                                    // 13: se rearma
        ServicioInventario.ConfirmarVenta(NuevaVenta(9), lote, Fabrica.Registro()); // 4: cruza otra vez
        Assert.Equal(2, existencia.Eventos.OfType<StockBajoMinimo>().Count());
    }

    [Fact]
    public void Stock_inicial_genera_kardex_tipo_inicial()
    {
        var existencia = new Existencia(_central.Id, _tornillo.Id);
        var resultado = ServicioInventario.RegistrarStockInicial(existencia, 50, 3.5m, Fabrica.Registro());

        Assert.Equal(TipoMovimientoInventario.Inicial, resultado.Kardex.Single().Tipo);
        Assert.Equal(175m, existencia.ValorInventario);
    }

    [Fact]
    public void Documento_confirmado_no_se_modifica_y_sin_lineas_no_se_confirma()
    {
        var vacia = new Venta(Fabrica.Cliente(), Fabrica.Hoy, Fabrica.Registro());
        Assert.Throws<DomainException>(() => ServicioInventario.ConfirmarVenta(vacia, new LoteExistencias([]), Fabrica.Registro()));

        var lote = new LoteExistencias([]);
        var recepcion = Recibir(lote, 1, 1m);
        Assert.Throws<DomainException>(() => recepcion.AgregarLinea(_tornillo, 1, 1, TasaIva.General));
    }

    [Fact]
    public void Cliente_inactivo_no_puede_comprar()
    {
        var cliente = Fabrica.Cliente();
        cliente.Desactivar();
        Assert.Throws<DomainException>(() => new Venta(cliente, Fabrica.Hoy, Fabrica.Registro()));
    }
}
