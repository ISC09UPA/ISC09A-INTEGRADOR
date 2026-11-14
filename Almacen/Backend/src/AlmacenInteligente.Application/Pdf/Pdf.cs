using AlmacenInteligente.Application.Abstracciones;
using AlmacenInteligente.Application.CasosDeUso;
using AlmacenInteligente.Application.Consultas;
using AlmacenInteligente.Application.Seguridad;
using AlmacenInteligente.Domain.Inventario;
using FluentValidation;

namespace AlmacenInteligente.Application.Pdf;

/// <summary>Un archivo generado, listo para descargarse desde la web o devolverse por la API.</summary>
public sealed record ArchivoDto(string Nombre, string TipoContenido, byte[] Contenido);

public enum TipoReporte
{
    General = 0,
    Ventas = 1,
    Compras = 2,
    Inventario = 3,
    Financiero = 4,
    Margen = 5,
}

/// <summary>Lo que necesita un reporte PDF. Las secciones que no aplican al tipo pedido van en null.</summary>
public sealed record DatosReporte(
    TipoReporte Tipo, DateOnly Desde, DateOnly Hasta, DateTimeOffset GeneradoEn, string GeneradoPor,
    ReporteVentasDto? Ventas, ReporteComprasDto? Compras, ReporteInventarioDto? Inventario,
    ReporteFinancieroDto? Financiero, IReadOnlyList<MargenProductoDto>? Margen);

/// <summary>Puerto: arma los PDF con formato (no capturas de pantalla), con gráficas en los reportes.</summary>
public interface IGeneradorPdf
{
    byte[] Comprobante(TipoDocumento tipo, DocumentoDetalleDto documento, DateTimeOffset generadoEn);
    byte[] Reporte(DatosReporte datos);
    byte[] ReporteMensual(Finanzas.ReporteMensualDto reporte, DateTimeOffset generadoEn, string generadoPor);
    byte[] Movimientos(Movimientos.FiltroMovimientos filtro, Movimientos.ReporteMovimientosDto reporte, DateTimeOffset generadoEn, string generadoPor);
}

[RequierePermiso(Permisos.Ventas.Ver)]
public sealed record ComprobanteVentaPdf(Guid Id) : ISolicitud<ArchivoDto>;

[RequierePermiso(Permisos.Recepciones.Ver)]
public sealed record ComprobanteRecepcionPdf(Guid Id) : ISolicitud<ArchivoDto>;

[RequierePermiso(Permisos.Traspasos.Ver)]
public sealed record ComprobanteTraspasoPdf(Guid Id) : ISolicitud<ArchivoDto>;

[RequierePermiso(Permisos.Reportes.Ver)]
public sealed record ReportePdf(TipoReporte Tipo, DateOnly Desde, DateOnly Hasta) : ISolicitud<ArchivoDto>;

internal sealed class ReportePdfValidador : AbstractValidator<ReportePdf>
{
    public ReportePdfValidador()
    {
        RuleFor(x => x.Tipo).IsInEnum().WithMessage("El tipo de reporte no existe.");
        RuleFor(x => x).Must(x => x.Desde <= x.Hasta && x.Hasta.DayNumber - x.Desde.DayNumber <= 366 * 3)
            .WithName("Periodo").WithMessage("El periodo no es válido (la fecha inicial va antes que la final, máximo 3 años).");
    }
}

internal sealed class PdfManejador(IConsultas consultas, IGeneradorPdf generador, IReloj reloj, IUsuarioActual usuario,
        Usuarios.IGestorUsuarios usuarios) :
    IManejador<ComprobanteVentaPdf, ArchivoDto>,
    IManejador<ComprobanteRecepcionPdf, ArchivoDto>,
    IManejador<ComprobanteTraspasoPdf, ArchivoDto>,
    IManejador<ReportePdf, ArchivoDto>
{
    private const string Pdf = "application/pdf";

    public async Task<ArchivoDto> ManejarAsync(ComprobanteVentaPdf s, CancellationToken ct)
    {
        var venta = await consultas.ObtenerVentaAsync(s.Id, ct) ?? throw new NoEncontradoException("la venta", s.Id);
        return new ArchivoDto($"VTA-{venta.Folio:D5}.pdf", Pdf, generador.Comprobante(TipoDocumento.Venta, venta, reloj.Ahora));
    }

    public async Task<ArchivoDto> ManejarAsync(ComprobanteRecepcionPdf s, CancellationToken ct)
    {
        var recepcion = await consultas.ObtenerRecepcionAsync(s.Id, ct) ?? throw new NoEncontradoException("la recepción", s.Id);
        return new ArchivoDto($"REC-{recepcion.Folio:D5}.pdf", Pdf, generador.Comprobante(TipoDocumento.Recepcion, recepcion, reloj.Ahora));
    }

    public async Task<ArchivoDto> ManejarAsync(ComprobanteTraspasoPdf s, CancellationToken ct)
    {
        var traspaso = await consultas.ObtenerTraspasoAsync(s.Id, ct) ?? throw new NoEncontradoException("el traspaso", s.Id);
        return new ArchivoDto($"TRA-{traspaso.Folio:D5}.pdf", Pdf, generador.Comprobante(TipoDocumento.Traspaso, traspaso, reloj.Ahora));
    }

    public async Task<ArchivoDto> ManejarAsync(ReportePdf s, CancellationToken ct)
    {
        bool Incluye(TipoReporte t) => s.Tipo == TipoReporte.General || s.Tipo == t;

        var autor = usuario.Id is { } id && await usuarios.ObtenerAsync(id, ct) is { } u ? u.Nombre : "—";
        var datos = new DatosReporte(s.Tipo, s.Desde, s.Hasta, reloj.Ahora, autor,
            Incluye(TipoReporte.Ventas) ? await consultas.ReporteVentasAsync(s.Desde, s.Hasta, ct) : null,
            Incluye(TipoReporte.Compras) ? await consultas.ReporteComprasAsync(s.Desde, s.Hasta, ct) : null,
            Incluye(TipoReporte.Inventario) ? await consultas.ReporteInventarioAsync(ct) : null,
            Incluye(TipoReporte.Financiero) ? await consultas.ReporteFinancieroAsync(s.Desde, s.Hasta, ct) : null,
            Incluye(TipoReporte.Margen) ? await consultas.ReporteMargenAsync(s.Desde, s.Hasta, ct) : null);

        var nombre = $"reporte-{s.Tipo.ToString().ToLowerInvariant()}-{s.Desde:yyyyMMdd}-{s.Hasta:yyyyMMdd}.pdf";
        return new ArchivoDto(nombre, Pdf, generador.Reporte(datos));
    }
}
