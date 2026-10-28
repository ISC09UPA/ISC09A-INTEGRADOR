using System.Globalization;
using System.Text;
using AlmacenInteligente.Application.Abstracciones;
using AlmacenInteligente.Application.CasosDeUso;
using AlmacenInteligente.Application.Pdf;
using AlmacenInteligente.Application.Seguridad;
using AlmacenInteligente.Application.Terceros;
using AlmacenInteligente.Domain.Inventario;
using FluentValidation;

namespace AlmacenInteligente.Application.Movimientos;

// Reporte de movimientos de inventario al estilo de Dolibarr: todos los movimientos de todos los productos
// y almacenes con filtros, totales de entradas y salidas, resumen por producto y exportación.

public sealed record FiltroMovimientos(
    DateOnly Desde, DateOnly Hasta, Guid? AlmacenId = null, Guid? ProductoId = null,
    TipoMovimientoInventario? Tipo = null, string? Busqueda = null, int NumeroPagina = 1, int Tamano = 50);

public sealed record MovimientoDto(
    Guid Id, DateTimeOffset Fecha, string Almacen, Guid ProductoId, string Referencia, string Descripcion, string Unidad,
    TipoMovimientoInventario Tipo, decimal Cantidad, decimal CostoUnitario, decimal Saldo,
    TipoDocumento? DocumentoTipo, Guid? DocumentoId, long? Folio, string Usuario)
{
    public decimal Valor => Math.Round(Cantidad * CostoUnitario, 2, MidpointRounding.AwayFromZero);
}

/// <summary>Por producto y almacén: existencia al inicio del periodo, lo que entró, lo que salió y con qué cerró.</summary>
public sealed record ResumenProductoDto(
    string Almacen, string Referencia, string Descripcion, string Unidad,
    decimal Inicial, decimal Entradas, decimal Salidas, decimal Final, decimal ValorEntradas, decimal ValorSalidas);

public sealed record ReporteMovimientosDto(
    Pagina<MovimientoDto> Movimientos, int NumeroMovimientos,
    decimal Entradas, decimal Salidas, decimal ValorEntradas, decimal ValorSalidas,
    IReadOnlyList<ResumenProductoDto> PorProducto);

/// <summary>Puerto de lectura del reporte (lo implementa la infraestructura sobre el kardex).</summary>
public interface IConsultasMovimientos
{
    Task<ReporteMovimientosDto> ReporteAsync(FiltroMovimientos filtro, bool todos, CancellationToken ct);
}

[RequierePermiso(Permisos.Almacenes.VerExistencias)]
public sealed record ReporteMovimientos(FiltroMovimientos Filtro) : ISolicitud<ReporteMovimientosDto>;

[RequierePermiso(Permisos.Almacenes.VerExistencias)]
public sealed record MovimientosPdf(FiltroMovimientos Filtro) : ISolicitud<ArchivoDto>;

[RequierePermiso(Permisos.Almacenes.VerExistencias)]
public sealed record MovimientosCsv(FiltroMovimientos Filtro) : ISolicitud<ArchivoDto>;

internal static class ReglasFiltro
{
    public static void Aplicar<T>(AbstractValidator<T> v, Func<T, FiltroMovimientos> filtro) =>
        v.RuleFor(x => filtro(x)).Must(f => f.Desde <= f.Hasta && f.Hasta.DayNumber - f.Desde.DayNumber <= 366 * 3)
            .WithName("Periodo").WithMessage("El periodo no es válido (la fecha inicial va antes que la final, máximo 3 años).");
}

internal sealed class ReporteMovimientosValidador : AbstractValidator<ReporteMovimientos>
{
    public ReporteMovimientosValidador() => ReglasFiltro.Aplicar(this, x => x.Filtro);
}

internal sealed class MovimientosPdfValidador : AbstractValidator<MovimientosPdf>
{
    public MovimientosPdfValidador() => ReglasFiltro.Aplicar(this, x => x.Filtro);
}

internal sealed class MovimientosCsvValidador : AbstractValidator<MovimientosCsv>
{
    public MovimientosCsvValidador() => ReglasFiltro.Aplicar(this, x => x.Filtro);
}

internal sealed class MovimientosManejador(
        IConsultasMovimientos consultas, IGeneradorPdf pdf, IReloj reloj, IUsuarioActual usuario, Usuarios.IGestorUsuarios usuarios) :
    IManejador<ReporteMovimientos, ReporteMovimientosDto>,
    IManejador<MovimientosPdf, ArchivoDto>,
    IManejador<MovimientosCsv, ArchivoDto>
{
    private static readonly CultureInfo Mx = CultureInfo.GetCultureInfo("es-MX");

    public Task<ReporteMovimientosDto> ManejarAsync(ReporteMovimientos s, CancellationToken ct) =>
        consultas.ReporteAsync(s.Filtro, todos: false, ct);

    public async Task<ArchivoDto> ManejarAsync(MovimientosPdf s, CancellationToken ct)
    {
        var reporte = await consultas.ReporteAsync(s.Filtro, todos: true, ct);
        var autor = usuario.Id is { } id && await usuarios.ObtenerAsync(id, ct) is { } u ? u.Nombre : "—";
        var contenido = pdf.Movimientos(s.Filtro, reporte, reloj.Ahora, autor);
        return new ArchivoDto($"movimientos-{s.Filtro.Desde:yyyyMMdd}-{s.Filtro.Hasta:yyyyMMdd}.pdf", "application/pdf", contenido);
    }

    /// <summary>CSV con BOM y separador coma para que Excel en español lo abra con acentos correctos.</summary>
    public async Task<ArchivoDto> ManejarAsync(MovimientosCsv s, CancellationToken ct)
    {
        var reporte = await consultas.ReporteAsync(s.Filtro, todos: true, ct);
        var csv = new StringBuilder();
        csv.AppendLine("Fecha,Almacen,Referencia,Descripcion,Unidad,Movimiento,Documento,Cantidad,Costo unitario,Valor,Saldo,Usuario");
        foreach (var m in reporte.Movimientos.Elementos)
        {
            var documento = m.DocumentoTipo is { } t ? $"{EtiquetasMovimiento.Prefijo(t)}-{m.Folio:D5}" : "";
            csv.AppendLine(string.Join(",",
                Celda(m.Fecha.ToOffset(TimeSpan.FromHours(-6)).ToString("yyyy-MM-dd HH:mm", Mx)), Celda(m.Almacen), Celda(m.Referencia),
                Celda(m.Descripcion), Celda(m.Unidad), Celda(EtiquetasMovimiento.Nombre(m.Tipo)), Celda(documento),
                Numero(m.Cantidad), Numero(m.CostoUnitario), Numero(m.Valor), Numero(m.Saldo), Celda(m.Usuario)));
        }
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return new ArchivoDto($"movimientos-{s.Filtro.Desde:yyyyMMdd}-{s.Filtro.Hasta:yyyyMMdd}.csv", "text/csv", bytes);
    }

    private static string Celda(string? texto) => $"\"{(texto ?? "").Replace("\"", "\"\"")}\"";

    private static string Numero(decimal valor) => valor.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Textos para mostrar tipos de movimiento y documentos en CSV y PDF.</summary>
public static class EtiquetasMovimiento
{
    public static string Prefijo(TipoDocumento tipo) => tipo switch
    {
        TipoDocumento.Recepcion => "REC",
        TipoDocumento.Venta => "VTA",
        _ => "TRA",
    };

    public static string Nombre(TipoMovimientoInventario tipo) => tipo switch
    {
        TipoMovimientoInventario.Inicial => "Stock inicial",
        TipoMovimientoInventario.Recepcion => "Recepción",
        TipoMovimientoInventario.Venta => "Venta",
        TipoMovimientoInventario.TraspasoSalida => "Traspaso (salida)",
        TipoMovimientoInventario.TraspasoEntrada => "Traspaso (entrada)",
        TipoMovimientoInventario.Ajuste => "Ajuste",
        TipoMovimientoInventario.CancelacionRecepcion => "Cancelación de recepción",
        TipoMovimientoInventario.CancelacionVenta => "Cancelación de venta",
        _ => tipo.ToString(),
    };
}
