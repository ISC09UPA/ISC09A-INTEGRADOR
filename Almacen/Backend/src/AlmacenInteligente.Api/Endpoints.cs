using AlmacenInteligente.Application.Abstracciones;
using AlmacenInteligente.Application.CasosDeUso;
using AlmacenInteligente.Application.Finanzas;
using AlmacenInteligente.Application.Movimientos;
using AlmacenInteligente.Application.Pdf;
using AlmacenInteligente.Application.Usuarios;
using AlmacenInteligente.Domain.Inventario;
using AlmacenInteligente.Infrastructure.Seguridad;
using Microsoft.AspNetCore.Identity;

namespace AlmacenInteligente.Api;

public sealed record SolicitudToken(string Correo, string Contrasena);

internal static class Endpoints
{
    /// <summary>Inicio de sesión para la app móvil: mismos usuarios, roles y permisos que la web.</summary>
    public static void MapCuenta(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/cuenta/token", async (SolicitudToken s, UserManager<Usuario> usuarios, SignInManager<Usuario> sesion,
                IEmisorTokens emisor, CancellationToken ct) =>
            {
                var usuario = await usuarios.FindByEmailAsync(s.Correo.Trim());
                if (usuario is null)
                    return Results.Problem("Correo o contraseña incorrectos, o la cuenta está desactivada.", statusCode: StatusCodes.Status401Unauthorized);

                // Respeta bloqueo por intentos fallidos y usuarios desactivados (GestorInicioSesion).
                var resultado = await sesion.CheckPasswordSignInAsync(usuario, s.Contrasena, lockoutOnFailure: true);
                if (!resultado.Succeeded)
                    return Results.Problem(resultado.IsLockedOut
                            ? "La cuenta está bloqueada por varios intentos fallidos. Intenta de nuevo en unos minutos."
                            : "Correo o contraseña incorrectos, o la cuenta está desactivada.",
                        statusCode: StatusCodes.Status401Unauthorized);

                return Results.Ok(await emisor.EmitirAsync(usuario.Id, ct));
            })
            .AllowAnonymous()
            .WithTags("Cuenta")
            .WithName("ObtenerToken")
            .WithSummary("Devuelve un token JWT para usar en Authorization: Bearer.");


    /// <summary>Comprobantes y reportes en PDF, los mismos que descarga la web.</summary>
    public static void MapPdf(this IEndpointRouteBuilder app)
    {
        var pdf = app.MapGroup("/api/pdf").RequireAuthorization().WithTags("PDF");
        pdf.MapGet("/ventas/{id:guid}", (Guid id, IMediador m, CancellationToken ct) => Archivo(m, new ComprobanteVentaPdf(id), ct))
            .WithName("ComprobanteVenta");
        pdf.MapGet("/recepciones/{id:guid}", (Guid id, IMediador m, CancellationToken ct) => Archivo(m, new ComprobanteRecepcionPdf(id), ct))
            .WithName("ComprobanteRecepcion");
        pdf.MapGet("/traspasos/{id:guid}", (Guid id, IMediador m, CancellationToken ct) => Archivo(m, new ComprobanteTraspasoPdf(id), ct))
            .WithName("ComprobanteTraspaso");
        pdf.MapGet("/reportes/{tipo}", (TipoReporte tipo, DateOnly desde, DateOnly hasta, IMediador m, CancellationToken ct) =>
                Archivo(m, new ReportePdf(tipo, desde, hasta), ct))
            .WithName("ReportePdf")
            .WithSummary("tipo: General, Ventas, Compras, Inventario, Financiero o Margen. Fechas en formato yyyy-MM-dd.");
        pdf.MapGet("/movimientos", (DateOnly desde, DateOnly hasta, Guid? almacen, Guid? producto, TipoMovimientoInventario? tipo,
                string? busqueda, IMediador m, CancellationToken ct) =>
                Archivo(m, new MovimientosPdf(new FiltroMovimientos(desde, hasta, almacen, producto, tipo, busqueda)), ct))
            .WithName("MovimientosPdf");

        pdf.MapGet("/finanzas/mensual", (int? anio, IMediador m, CancellationToken ct) =>
                Archivo(m, new ReporteMensualFinancieroPdf(anio ?? DateTime.Today.Year), ct))
            .WithSummary("anio: año final como número, p. ej. 2026 (por defecto el año actual). Muestra ese año y los dos anteriores.")
            .WithName("ReporteMensualPdf");
        app.MapGet("/api/finanzas/mensual", (int? anio, int? anios, IMediador m, CancellationToken ct) =>
                m.EnviarAsync(new ReporteMensualFinanciero(anio ?? DateTime.Today.Year, anios ?? 3), ct))
            .RequireAuthorization().WithTags("Finanzas").WithName("ReporteMensual")
            .WithSummary("anio: año final como número, p. ej. 2026 (por defecto el actual); anios: cuántos años comparar (1 a 5, por defecto 3). Debe = egresos, Haber = ingresos.");

        var movimientos = app.MapGroup("/api/movimientos").RequireAuthorization().WithTags("Movimientos");
        movimientos.MapGet("", (DateOnly desde, DateOnly hasta, Guid? almacen, Guid? producto, TipoMovimientoInventario? tipo,
                string? busqueda, int? pagina, int? tamano, IMediador m, CancellationToken ct) =>
                m.EnviarAsync(new ReporteMovimientos(new FiltroMovimientos(desde, hasta, almacen, producto, tipo, busqueda, pagina ?? 1, tamano ?? 50)), ct))
            .WithName("ReporteMovimientos")
            .WithSummary("Movimientos paginados, totales y resumen por producto (JSON).");
        movimientos.MapGet("/csv", (DateOnly desde, DateOnly hasta, Guid? almacen, Guid? producto, TipoMovimientoInventario? tipo,
                string? busqueda, IMediador m, CancellationToken ct) =>
                Archivo(m, new MovimientosCsv(new FiltroMovimientos(desde, hasta, almacen, producto, tipo, busqueda)), ct))
            .WithName("MovimientosCsv");
    }

    private static async Task<IResult> Archivo(IMediador mediador, ISolicitud<ArchivoDto> solicitud, CancellationToken ct)
    {
        var archivo = await mediador.EnviarAsync(solicitud, ct);
        return Results.File(archivo.Contenido, archivo.TipoContenido, archivo.Nombre);
    }
}
