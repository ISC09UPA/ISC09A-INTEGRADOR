using System.Globalization;
using AlmacenInteligente.Domain.Documentos;
using AlmacenInteligente.Domain.Inventario;
using AlmacenInteligente.Domain.Productos;
using MudBlazor;

namespace AlmacenInteligente.Web.Components.Comunes;

/// <summary>Cómo se muestran importes, cantidades, fechas y estados en todas las pantallas.</summary>
public static class Formato
{
    public static readonly CultureInfo Mx = CultureInfo.GetCultureInfo("es-MX");
    private static readonly TimeZoneInfo ZonaMexico = TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");

    public static string Dinero(decimal valor) => valor.ToString("C2", Mx);

    public static string Cantidad(decimal valor) => valor.ToString(decimal.Truncate(valor) == valor ? "N0" : "N3", Mx);

    /// <summary>Costos unitarios: 2 decimales salvo que el costo promedio tenga más.</summary>
    public static string Costo(decimal valor) => valor.ToString(Math.Round(valor, 2) == valor ? "C2" : "C4", Mx);

    public static string Porcentaje(decimal fraccion) => (fraccion * 100).ToString("0.##", Mx) + " %";

    public static string Fecha(DateOnly fecha) => fecha.ToString("dd MMM yyyy", Mx);

    public static string FechaHora(DateTimeOffset fecha) =>
        TimeZoneInfo.ConvertTime(fecha, ZonaMexico).ToString("dd MMM yyyy HH:mm", Mx);

    public static DateOnly Hoy => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, ZonaMexico).DateTime);

    public static string Folio(string prefijo, long folio) => $"{prefijo}-{folio:D5}";

    public static (string Texto, Color Color) Estado(EstadoDocumento estado) => estado switch
    {
        EstadoDocumento.Confirmado => ("Confirmado", Color.Success),
        EstadoDocumento.Cancelado => ("Cancelado", Color.Error),
        _ => ("Borrador", Color.Default),
    };

    public static (string Texto, Color Color) Estado(EstadoAprobacion estado) => estado switch
    {
        EstadoAprobacion.Aprobado => ("Aprobado", Color.Success),
        EstadoAprobacion.Rechazado => ("Rechazado", Color.Error),
        _ => ("Pendiente", Color.Warning),
    };

    public static string Movimiento(TipoMovimientoInventario tipo) => tipo switch
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

    public static string Prefijo(TipoDocumento tipo) => tipo switch
    {
        TipoDocumento.Recepcion => "REC",
        TipoDocumento.Venta => "VTA",
        _ => "TRA",
    };

    public static string Ruta(TipoDocumento tipo, Guid id) => tipo switch
    {
        TipoDocumento.Recepcion => $"recepciones/{id}",
        TipoDocumento.Venta => $"ventas/{id}",
        _ => $"traspasos/{id}",
    };

    public static readonly IReadOnlyList<(decimal Valor, string Texto)> TasasIva =
    [
        (0.16m, "16 %"),
        (0.08m, "8 % (frontera)"),
        (0m, "0 %"),
    ];
}
