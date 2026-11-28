using AlmacenInteligente.Application.Abstracciones;
using AlmacenInteligente.Application.CasosDeUso;
using AlmacenInteligente.Application.Finanzas;
using AlmacenInteligente.Application.Movimientos;
using AlmacenInteligente.Application.Pdf;
using AlmacenInteligente.Domain.Common;
using AlmacenInteligente.Domain.Inventario;

namespace AlmacenInteligente.Web.Seguridad;

/// <summary>
/// PDF y CSV servidos por HTTP con la cookie de sesión, para abrirlos en otra pestaña o descargarlos.
/// Pasan por el mismo mediador (y los mismos permisos) que el resto de la app.
/// </summary>
internal static class EndpointsPdf
{
    public static void MapPdf(this IEndpointRouteBuilder app)
    {
        var pdf = app.MapGroup("/pdf").RequireAuthorization();
        pdf.MapGet("/ventas/{id:guid}", (Guid id, bool? descargar, HttpContext ctx) => Generar(ctx, new ComprobanteVentaPdf(id), descargar));
        pdf.MapGet("/recepciones/{id:guid}", (Guid id, bool? descargar, HttpContext ctx) => Generar(ctx, new ComprobanteRecepcionPdf(id), descargar));
        pdf.MapGet("/traspasos/{id:guid}", (Guid id, bool? descargar, HttpContext ctx) => Generar(ctx, new ComprobanteTraspasoPdf(id), descargar));
        pdf.MapGet("/reportes/{tipo}", (TipoReporte tipo, DateOnly desde, DateOnly hasta, bool? descargar, HttpContext ctx) =>
            Generar(ctx, new ReportePdf(tipo, desde, hasta), descargar));
        pdf.MapGet("/movimientos", (DateOnly desde, DateOnly hasta, Guid? almacen, Guid? producto, TipoMovimientoInventario? tipo,
                string? busqueda, bool? descargar, HttpContext ctx) =>
            Generar(ctx, new MovimientosPdf(new FiltroMovimientos(desde, hasta, almacen, producto, tipo, busqueda)), descargar));

        pdf.MapGet("/finanzas/mensual", (int anio, bool? descargar, HttpContext ctx) =>
            Generar(ctx, new ReporteMensualFinancieroPdf(anio), descargar));

        app.MapGet("/csv/movimientos", (DateOnly desde, DateOnly hasta, Guid? almacen, Guid? producto, TipoMovimientoInventario? tipo,
                string? busqueda, HttpContext ctx) =>
            Generar(ctx, new MovimientosCsv(new FiltroMovimientos(desde, hasta, almacen, producto, tipo, busqueda)), descargar: true))
            .RequireAuthorization();
    }

    private static async Task<IResult> Generar(HttpContext ctx, ISolicitud<ArchivoDto> solicitud, bool? descargar)
    {
        var servicios = ctx.RequestServices;
        ((UsuarioActualPrincipal)servicios.GetRequiredService<IUsuarioActual>()).Principal = ctx.User;
        try
        {
            var archivo = await servicios.GetRequiredService<IMediador>().EnviarAsync(solicitud, ctx.RequestAborted);
            // Sin nombre el navegador lo muestra en la pestaña; con nombre lo descarga.
            return descargar == true
                ? Results.File(archivo.Contenido, archivo.TipoContenido, archivo.Nombre)
                : Results.File(archivo.Contenido, archivo.TipoContenido);
        }
        catch (NoEncontradoException ex)
        {
            return Results.NotFound(ex.Message);
        }
        catch (AccesoDenegadoException)
        {
            return Results.Forbid();
        }
        catch (Exception ex) when (ex is DomainException or ValidacionException)
        {
            return Results.BadRequest(ex is ValidacionException v ? string.Join(" ", v.Errores.SelectMany(e => e.Value)) : ex.Message);
        }
    }
}
