using AlmacenInteligente.Application.Abstracciones;
using AlmacenInteligente.Application.Consultas;
using AlmacenInteligente.Application.Pdf;
using AlmacenInteligente.Application.Seguridad;
using AlmacenInteligente.Application.Usuarios;
using AlmacenInteligente.Domain.Inventario;
using AlmacenInteligente.Domain.Productos;
using AlmacenInteligente.Domain.Recepciones;
using AlmacenInteligente.Domain.Ventas;

namespace AlmacenInteligente.Application.Notificaciones;

/// <summary>
/// Correos que salen de los eventos de dominio. Si el destinatario ya no existe o no tiene correo, el evento se da por atendido
/// (reintentar no lo va a arreglar); si el envío falla, la excepción sube y el outbox lo reintenta.
/// </summary>
internal sealed class CorreosNotificacion(
    IEmailSender correo, IGestorUsuarios usuarios, IProductoRepositorio productos, IAlmacenRepositorio almacenes,
    IConsultas consultas, IGeneradorPdf pdf, IReloj reloj, OpcionesNotificaciones opciones) :
    IManejadorEvento<ProductoPendienteDeAprobacion>,
    IManejadorEvento<ProductoAprobado>,
    IManejadorEvento<ProductoRechazado>,
    IManejadorEvento<StockBajoMinimo>,
    IManejadorEvento<VentaConfirmada>,
    IManejadorEvento<RecepcionConfirmada>
{
    /// <summary>El gerente elegido recibe la solicitud con el enlace directo para aprobar o rechazar.</summary>
    public async Task ManejarAsync(ProductoPendienteDeAprobacion e, CancellationToken ct)
    {
        if (await usuarios.ObtenerAsync(e.GerenteId, ct) is not { Activo: true } gerente || await productos.ObtenerAsync(e.ProductoId, ct) is not { } producto)
            return;
        var solicitante = await usuarios.ObtenerAsync(e.SolicitanteId, ct);

        await Enviar([gerente.Correo], $"Autoriza el producto {producto.Referencia}",
            "Producto pendiente de tu autorización",
            [$"Hola {gerente.Nombre}, {solicitante?.Nombre ?? "un encargado"} dio de alta un producto y te eligió para autorizarlo.",
             "Mientras no lo autorices no se puede usar en recepciones, ventas ni traspasos."],
            [("Referencia", producto.Referencia), ("Descripción", producto.Descripcion), ("Solicitado", PlantillaCorreo.Fecha(e.OcurrioEn))],
            ("Revisar solicitud", opciones.Enlace($"productos/aprobaciones/{producto.Id}")), ct);
    }

    public async Task ManejarAsync(ProductoAprobado e, CancellationToken ct)
    {
        if (await usuarios.ObtenerAsync(e.SolicitanteId, ct) is not { Activo: true } solicitante || await productos.ObtenerAsync(e.ProductoId, ct) is not { } producto)
            return;
        var gerente = await usuarios.ObtenerAsync(e.GerenteId, ct);

        await Enviar([solicitante.Correo], $"Producto {producto.Referencia} autorizado",
            "Tu producto fue autorizado",
            [$"{gerente?.Nombre ?? "El gerente"} autorizó el producto. Ya lo puedes usar en recepciones, ventas y traspasos."],
            [("Referencia", producto.Referencia), ("Descripción", producto.Descripcion), ("Autorizado", PlantillaCorreo.Fecha(e.OcurrioEn))],
            ("Ver productos", opciones.Enlace("productos")), ct);
    }

    public async Task ManejarAsync(ProductoRechazado e, CancellationToken ct)
    {
        if (await usuarios.ObtenerAsync(e.SolicitanteId, ct) is not { Activo: true } solicitante || await productos.ObtenerAsync(e.ProductoId, ct) is not { } producto)
            return;
        var gerente = await usuarios.ObtenerAsync(e.GerenteId, ct);

        await Enviar([solicitante.Correo], $"Producto {producto.Referencia} rechazado",
            "Tu producto fue rechazado",
            [$"{gerente?.Nombre ?? "El gerente"} rechazó el producto."],
            [("Referencia", producto.Referencia), ("Descripción", producto.Descripcion), ("Motivo", e.Motivo), ("Rechazado", PlantillaCorreo.Fecha(e.OcurrioEn))],
            ("Ver productos", opciones.Enlace("productos")), ct);
    }

    /// <summary>Va a todos los usuarios activos con el permiso de alertas de bajo stock (encargado y gerente por defecto).</summary>
    public async Task ManejarAsync(StockBajoMinimo e, CancellationToken ct)
    {
        var destinatarios = (await usuarios.ListarConPermisoAsync(Permisos.Alertas.RecibirBajoStock, ct)).Select(u => u.Correo).ToList();
        if (destinatarios.Count == 0 || await productos.ObtenerAsync(e.ProductoId, ct) is not { } producto)
            return;
        var almacen = await almacenes.ObtenerAsync(e.AlmacenId, ct);

        await Enviar(destinatarios, $"Stock bajo: {producto.Referencia} en {almacen?.Nombre ?? "un almacén"}",
            "Existencia en o por debajo del mínimo",
            ["Un producto llegó a su stock mínimo. Considera surtirlo o hacer un traspaso desde otro almacén."],
            [("Producto", $"{producto.Referencia} · {producto.Descripcion}"), ("Almacén", almacen?.Nombre ?? "—"),
             ("Existencia", PlantillaCorreo.Cantidad(e.Cantidad)), ("Mínimo", PlantillaCorreo.Cantidad(e.StockMinimo)),
             ("Fecha", PlantillaCorreo.Fecha(e.OcurrioEn))],
            ("Ver existencias bajo mínimo", opciones.Enlace("existencias")), ct);
    }

    /// <summary>El cliente recibe el comprobante PDF de su compra.</summary>
    public async Task ManejarAsync(VentaConfirmada e, CancellationToken ct)
    {
        if (!opciones.ComprobanteVentaAlCliente || await consultas.ObtenerVentaAsync(e.VentaId, ct) is not { TerceroCorreo: { Length: > 0 } para } venta)
            return;

        var folio = $"VTA-{venta.Folio:D5}";
        var mensaje = new MensajeCorreo([para], $"Comprobante de tu compra {folio}",
            PlantillaCorreo.Html(opciones.NombreEmpresa, "Gracias por tu compra",
                [$"Hola {venta.Tercero}, adjuntamos el comprobante de tu compra."],
                [("Folio", folio), ("Fecha", venta.Fecha.ToString("dd/MM/yyyy")), ("Total", venta.Total.ToString("C2", System.Globalization.CultureInfo.GetCultureInfo("es-MX")))]),
            [new Adjunto($"{folio}.pdf", "application/pdf", pdf.Comprobante(TipoDocumento.Venta, venta, reloj.Ahora))]);
        await correo.EnviarAsync(mensaje, ct);
    }

    /// <summary>El proveedor recibe el comprobante PDF de la mercancía que se le recibió.</summary>
    public async Task ManejarAsync(RecepcionConfirmada e, CancellationToken ct)
    {
        if (!opciones.ComprobanteRecepcionAlProveedor || await consultas.ObtenerRecepcionAsync(e.RecepcionId, ct) is not { TerceroCorreo: { Length: > 0 } para } recepcion)
            return;

        var folio = $"REC-{recepcion.Folio:D5}";
        var mensaje = new MensajeCorreo([para], $"Comprobante de recepción {folio}",
            PlantillaCorreo.Html(opciones.NombreEmpresa, "Recibimos tu mercancía",
                [$"Hola {recepcion.Tercero}, adjuntamos el comprobante de la mercancía que recibimos."],
                [("Folio", folio), ("Fecha", recepcion.Fecha.ToString("dd/MM/yyyy")), ("Almacén", recepcion.Almacen), ("Total", recepcion.Total.ToString("C2", System.Globalization.CultureInfo.GetCultureInfo("es-MX")))]),
            [new Adjunto($"{folio}.pdf", "application/pdf", pdf.Comprobante(TipoDocumento.Recepcion, recepcion, reloj.Ahora))]);
        await correo.EnviarAsync(mensaje, ct);
    }

    private Task Enviar(IReadOnlyList<string> para, string asunto, string titulo, IEnumerable<string> parrafos,
        IEnumerable<(string, string)> datos, (string, string?) boton, CancellationToken ct)
    {
        var validos = para.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (validos.Count == 0)
            return Task.CompletedTask;
        return correo.EnviarAsync(new MensajeCorreo(validos, asunto,
            PlantillaCorreo.Html(opciones.NombreEmpresa, titulo, parrafos, datos, boton)), ct);
    }
}
