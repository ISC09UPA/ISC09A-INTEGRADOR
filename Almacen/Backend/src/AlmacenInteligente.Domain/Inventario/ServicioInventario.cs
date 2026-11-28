using AlmacenInteligente.Domain.Common;
using AlmacenInteligente.Domain.Finanzas;
using AlmacenInteligente.Domain.Recepciones;
using AlmacenInteligente.Domain.Ventas;

namespace AlmacenInteligente.Domain.Inventario;

/// <summary>Lo que una operación de inventario produce y la capa de aplicación debe guardar en la misma transacción.</summary>
public sealed record ResultadoInventario(
    IReadOnlyList<MovimientoInventario> Kardex,
    MovimientoFinanciero? Financiero);

/// <summary>
/// Reglas que cruzan documentos y existencias: confirmar y cancelar recepciones y ventas,
/// confirmar traspasos y registrar stock inicial. Todo movimiento de existencia pasa por aquí y deja kardex.
/// </summary>
public static class ServicioInventario
{
    public static ResultadoInventario RegistrarStockInicial(
        Existencia existencia, decimal cantidad, decimal costoUnitario, Auditoria registro)
    {
        var movimiento = existencia.Entrar(TipoMovimientoInventario.Inicial, cantidad, costoUnitario, null, registro);
        return new ResultadoInventario([movimiento], null);
    }

    public static ResultadoInventario ConfirmarRecepcion(Recepcion recepcion, LoteExistencias lote, Auditoria registro)
    {
        recepcion.MarcarConfirmado(registro);
        var documento = new DocumentoRef(TipoDocumento.Recepcion, recepcion.Id);

        var kardex = recepcion.Lineas
            .Select(l => lote.ParaEntrada(recepcion.AlmacenId, l.ProductoId)
                .Entrar(TipoMovimientoInventario.Recepcion, l.Cantidad, l.CostoUnitario, documento, registro))
            .ToList();

        recepcion.AlConfirmar();
        var egreso = MovimientoFinanciero.Registrar(
            NaturalezaFinanciera.Egreso, documento, recepcion.ProveedorId, recepcion.Importe, registro);
        return new ResultadoInventario(kardex, egreso);
    }

    /// <summary>Falla si parte de lo recibido ya salió del almacén y no alcanza para revertirlo.</summary>
    public static ResultadoInventario CancelarRecepcion(Recepcion recepcion, LoteExistencias lote, Auditoria registro, string motivo)
    {
        AsegurarDisponible(lote, recepcion.Lineas.Select(l => (recepcion.AlmacenId, l.ProductoId, l.Cantidad)));
        recepcion.MarcarCancelado(registro, motivo);
        var documento = new DocumentoRef(TipoDocumento.Recepcion, recepcion.Id);

        var kardex = recepcion.Lineas
            .Select(l => lote.ParaSalida(recepcion.AlmacenId, l.ProductoId)
                .RevertirEntrada(TipoMovimientoInventario.CancelacionRecepcion, l.Cantidad, l.CostoUnitario, documento, registro))
            .ToList();

        recepcion.AlCancelar();
        var reverso = MovimientoFinanciero.Reverso(
            NaturalezaFinanciera.Egreso, documento, recepcion.ProveedorId, recepcion.Importe, registro);
        return new ResultadoInventario(kardex, reverso);
    }

    public static ResultadoInventario ConfirmarVenta(Venta venta, LoteExistencias lote, Auditoria registro)
    {
        AsegurarDisponible(lote, venta.Lineas.Select(l => (l.AlmacenId, l.ProductoId, l.Cantidad)));
        venta.MarcarConfirmado(registro);
        var documento = new DocumentoRef(TipoDocumento.Venta, venta.Id);

        var kardex = new List<MovimientoInventario>(venta.Lineas.Count);
        foreach (var linea in venta.Lineas)
        {
            var existencia = lote.ParaSalida(linea.AlmacenId, linea.ProductoId);
            linea.FijarCosto(existencia.CostoPromedio);
            kardex.Add(existencia.Salir(TipoMovimientoInventario.Venta, linea.Cantidad, documento, registro));
        }

        venta.AlConfirmar();
        var ingreso = MovimientoFinanciero.Registrar(
            NaturalezaFinanciera.Ingreso, documento, venta.ClienteId, venta.Importe, registro);
        return new ResultadoInventario(kardex, ingreso);
    }

    /// <summary>Repone cada línea a su almacén al costo con que salió.</summary>
    public static ResultadoInventario CancelarVenta(Venta venta, LoteExistencias lote, Auditoria registro, string motivo)
    {
        venta.MarcarCancelado(registro, motivo);
        var documento = new DocumentoRef(TipoDocumento.Venta, venta.Id);

        var kardex = venta.Lineas
            .Select(l => lote.ParaEntrada(l.AlmacenId, l.ProductoId)
                .Entrar(TipoMovimientoInventario.CancelacionVenta, l.Cantidad, l.CostoUnitario, documento, registro))
            .ToList();

        venta.AlCancelar();
        var reverso = MovimientoFinanciero.Reverso(
            NaturalezaFinanciera.Ingreso, documento, venta.ClienteId, venta.Importe, registro);
        return new ResultadoInventario(kardex, reverso);
    }

    public static ResultadoInventario ConfirmarTraspaso(Traspaso traspaso, LoteExistencias lote, Auditoria registro)
    {
        AsegurarDisponible(lote, traspaso.Lineas.Select(l => (traspaso.AlmacenOrigenId, l.ProductoId, l.Cantidad)));
        traspaso.MarcarConfirmado(registro);
        var documento = new DocumentoRef(TipoDocumento.Traspaso, traspaso.Id);

        var kardex = new List<MovimientoInventario>(traspaso.Lineas.Count * 2);
        foreach (var linea in traspaso.Lineas)
        {
            var origen = lote.ParaSalida(traspaso.AlmacenOrigenId, linea.ProductoId);
            linea.FijarCosto(origen.CostoPromedio);
            kardex.Add(origen.Salir(TipoMovimientoInventario.TraspasoSalida, linea.Cantidad, documento, registro));
            kardex.Add(lote.ParaEntrada(traspaso.AlmacenDestinoId, linea.ProductoId)
                .Entrar(TipoMovimientoInventario.TraspasoEntrada, linea.Cantidad, linea.CostoUnitario, documento, registro));
        }

        return new ResultadoInventario(kardex, null);
    }

    /// <summary>Valida todas las salidas antes de tocar nada, sumando líneas repetidas del mismo producto y almacén.</summary>
    private static void AsegurarDisponible(LoteExistencias lote, IEnumerable<(Guid AlmacenId, Guid ProductoId, decimal Cantidad)> salidas)
    {
        foreach (var grupo in salidas.GroupBy(s => (s.AlmacenId, s.ProductoId)))
        {
            var existencia = lote.ParaSalida(grupo.Key.AlmacenId, grupo.Key.ProductoId);
            var requerida = grupo.Sum(s => s.Cantidad);
            if (requerida > existencia.Cantidad)
                throw new ExistenciaInsuficienteException(existencia.AlmacenId, existencia.ProductoId, existencia.Cantidad, requerida);
        }
    }
}
