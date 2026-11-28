using AlmacenInteligente.Application.CasosDeUso;
using AlmacenInteligente.Application.Consultas;
using AlmacenInteligente.Application.Documentos;
using AlmacenInteligente.Application.Reportes;
using AlmacenInteligente.Domain.Documentos;
using AlmacenInteligente.Domain.Finanzas;

namespace AlmacenInteligente.Api;

/// <summary>Recepciones, ventas, traspasos, movimientos financieros y reportes (JSON).</summary>
internal static class EndpointsOperacion
{
    public static void MapOperacion(this IEndpointRouteBuilder app)
    {
        var recepciones = app.MapGroup("/api/recepciones").RequireAuthorization().WithTags("Recepciones");
        recepciones.MapGet("", (string? busqueda, EstadoDocumento? estado, DateOnly? desde, DateOnly? hasta, int? pagina, int? tamano,
                IMediador m, CancellationToken ct) =>
            m.EnviarAsync(new ListarRecepciones(Filtro(busqueda, estado, desde, hasta, pagina, tamano)), ct)).WithName("ListarRecepciones");
        recepciones.MapGet("/{id:guid}", (Guid id, IMediador m, CancellationToken ct) => m.EnviarAsync(new ObtenerRecepcion(id), ct)).WithName("ObtenerRecepcion");
        recepciones.MapPost("", async (RegistrarRecepcion s, IMediador m, CancellationToken ct) =>
        {
            var registrada = await m.EnviarAsync(s, ct);
            return Results.Created($"/api/recepciones/{registrada.Id}", registrada);
        }).WithName("RegistrarRecepcion").WithSummary("Se registra confirmada: suma existencias, recalcula costo promedio y registra el egreso. tasaIva como fracción (0.16).");
        recepciones.MapPost("/{id:guid}/cancelar", async (Guid id, Motivo motivo, IMediador m, CancellationToken ct) =>
        {
            await m.EnviarAsync(new CancelarRecepcion(id, motivo.Texto), ct);
            return Results.NoContent();
        }).WithName("CancelarRecepcion");

        var ventas = app.MapGroup("/api/ventas").RequireAuthorization().WithTags("Ventas");
        ventas.MapGet("", (string? busqueda, EstadoDocumento? estado, DateOnly? desde, DateOnly? hasta, int? pagina, int? tamano,
                IMediador m, CancellationToken ct) =>
            m.EnviarAsync(new ListarVentas(Filtro(busqueda, estado, desde, hasta, pagina, tamano)), ct)).WithName("ListarVentas");
        ventas.MapGet("/{id:guid}", (Guid id, IMediador m, CancellationToken ct) => m.EnviarAsync(new ObtenerVenta(id), ct)).WithName("ObtenerVenta");
        ventas.MapPost("", async (RegistrarVenta s, IMediador m, CancellationToken ct) =>
        {
            var registrada = await m.EnviarAsync(s, ct);
            return Results.Created($"/api/ventas/{registrada.Id}", registrada);
        }).WithName("RegistrarVenta").WithSummary("Cada línea indica su almacén de salida y el precio que captura el agente. Nunca deja existencias negativas.");
        ventas.MapPost("/{id:guid}/cancelar", async (Guid id, Motivo motivo, IMediador m, CancellationToken ct) =>
        {
            await m.EnviarAsync(new CancelarVenta(id, motivo.Texto), ct);
            return Results.NoContent();
        }).WithName("CancelarVenta");

        var traspasos = app.MapGroup("/api/traspasos").RequireAuthorization().WithTags("Traspasos");
        traspasos.MapGet("", (string? busqueda, EstadoDocumento? estado, DateOnly? desde, DateOnly? hasta, int? pagina, int? tamano,
                IMediador m, CancellationToken ct) =>
            m.EnviarAsync(new ListarTraspasos(Filtro(busqueda, estado, desde, hasta, pagina, tamano)), ct)).WithName("ListarTraspasos");
        traspasos.MapGet("/{id:guid}", (Guid id, IMediador m, CancellationToken ct) => m.EnviarAsync(new ObtenerTraspaso(id), ct)).WithName("ObtenerTraspaso");
        traspasos.MapPost("", async (RegistrarTraspaso s, IMediador m, CancellationToken ct) =>
        {
            var registrado = await m.EnviarAsync(s, ct);
            return Results.Created($"/api/traspasos/{registrado.Id}", registrado);
        }).WithName("RegistrarTraspaso");

        var finanzas = app.MapGroup("/api/finanzas").RequireAuthorization().WithTags("Finanzas");
        finanzas.MapGet("/movimientos", (DateOnly? desde, DateOnly? hasta, NaturalezaFinanciera? naturaleza, int? pagina, int? tamano,
                IMediador m, CancellationToken ct) =>
            m.EnviarAsync(new ListarMovimientosFinancieros(new FiltroFinanzas(desde, hasta, naturaleza, pagina ?? 1, tamano ?? 50)), ct))
            .WithName("ListarMovimientosFinancieros");

        var reportes = app.MapGroup("/api/reportes").RequireAuthorization().WithTags("Reportes");
        reportes.MapGet("/ventas", (DateOnly desde, DateOnly hasta, IMediador m, CancellationToken ct) => m.EnviarAsync(new ReporteVentas(desde, hasta), ct))
            .WithName("ReporteVentas");
        reportes.MapGet("/compras", (DateOnly desde, DateOnly hasta, IMediador m, CancellationToken ct) => m.EnviarAsync(new ReporteCompras(desde, hasta), ct))
            .WithName("ReporteCompras");
        reportes.MapGet("/financiero", (DateOnly desde, DateOnly hasta, IMediador m, CancellationToken ct) => m.EnviarAsync(new ReporteFinanciero(desde, hasta), ct))
            .WithName("ReporteFinanciero");
        reportes.MapGet("/inventario", (IMediador m, CancellationToken ct) => m.EnviarAsync(new ReporteInventario(), ct)).WithName("ReporteInventario");
        reportes.MapGet("/margen", (DateOnly desde, DateOnly hasta, IMediador m, CancellationToken ct) => m.EnviarAsync(new ReporteMargen(desde, hasta), ct))
            .WithName("ReporteMargen");
    }

    private static FiltroDocumentos Filtro(string? busqueda, EstadoDocumento? estado, DateOnly? desde, DateOnly? hasta, int? pagina, int? tamano) =>
        new(busqueda, estado, desde, hasta, pagina ?? 1, Math.Clamp(tamano ?? 20, 1, 200));
}
