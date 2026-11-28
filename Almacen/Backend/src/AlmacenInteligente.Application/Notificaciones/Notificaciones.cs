using System.Globalization;
using System.Net;
using System.Text;
using AlmacenInteligente.Domain.Common;

namespace AlmacenInteligente.Application.Notificaciones;

/// <summary>
/// Reacciona a un evento de dominio que ya quedó confirmado en la base. Lo invoca el procesador del outbox,
/// fuera de la petición del usuario: si falla (p. ej. el servicio de correo no responde) se reintenta después.
/// </summary>
public interface IManejadorEvento<in TEvento> where TEvento : IDomainEvent
{
    Task ManejarAsync(TEvento evento, CancellationToken ct);
}

/// <summary>Sección "Notificaciones" de appsettings.</summary>
public sealed record OpcionesNotificaciones
{
    /// <summary>URL pública del Blazor (sin "/" final) para los enlaces de los correos. Sin ella los correos no llevan botón.</summary>
    public string? UrlWeb { get; init; }

    public string NombreEmpresa { get; init; } = "Almacén inteligente";

    /// <summary>Envía al cliente el comprobante PDF cuando se confirma una venta.</summary>
    public bool ComprobanteVentaAlCliente { get; init; } = true;

    public string? Enlace(string ruta) =>
        string.IsNullOrWhiteSpace(UrlWeb) ? null : $"{UrlWeb.TrimEnd('/')}/{ruta.TrimStart('/')}";
}

/// <summary>HTML sencillo y compatible con clientes de correo (estilos en línea, sin CSS externo). Todo el texto se codifica.</summary>
public static class PlantillaCorreo
{
    private static readonly CultureInfo Mexico = CultureInfo.GetCultureInfo("es-MX");
    private static readonly TimeZoneInfo ZonaMexico = TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");

    public static string Html(string empresa, string titulo, IEnumerable<string> parrafos,
        IEnumerable<(string Etiqueta, string Valor)>? datos = null, (string Texto, string? Url)? boton = null)
    {
        static string E(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);

        var html = new StringBuilder();
        html.Append("<!doctype html><html lang=\"es\"><body style=\"margin:0;padding:24px;background:#f4f5f7;font-family:Segoe UI,Arial,sans-serif;color:#1f2937\">");
        html.Append("<table role=\"presentation\" width=\"100%\" style=\"max-width:560px;margin:0 auto;background:#ffffff;border-radius:8px;padding:24px\"><tr><td>");
        html.Append($"<p style=\"margin:0 0 4px;font-size:12px;color:#6b7280;text-transform:uppercase;letter-spacing:.05em\">{E(empresa)}</p>");
        html.Append($"<h1 style=\"margin:0 0 16px;font-size:20px\">{E(titulo)}</h1>");
        foreach (var p in parrafos)
            html.Append($"<p style=\"margin:0 0 12px;line-height:1.5\">{E(p)}</p>");

        var filas = datos?.ToList() ?? [];
        if (filas.Count > 0)
        {
            html.Append("<table role=\"presentation\" style=\"width:100%;border-collapse:collapse;margin:8px 0 16px\">");
            foreach (var (etiqueta, valor) in filas)
                html.Append($"<tr><td style=\"padding:6px 8px 6px 0;color:#6b7280;white-space:nowrap;vertical-align:top\">{E(etiqueta)}</td><td style=\"padding:6px 0\">{E(valor)}</td></tr>");
            html.Append("</table>");
        }

        if (boton is { Url: { } url } b)
            html.Append($"<p style=\"margin:20px 0 8px\"><a href=\"{E(url)}\" style=\"display:inline-block;background:#2563eb;color:#ffffff;text-decoration:none;padding:10px 18px;border-radius:6px\">{E(b.Texto)}</a></p>");

        html.Append("<p style=\"margin:24px 0 0;font-size:12px;color:#9ca3af\">Correo automático, no respondas a este mensaje.</p>");
        html.Append("</td></tr></table></body></html>");
        return html.ToString();
    }

    public static string Cantidad(decimal valor) => valor.ToString("#,##0.####", Mexico);

    public static string Fecha(DateTimeOffset fecha) => TimeZoneInfo.ConvertTime(fecha, ZonaMexico).ToString("d 'de' MMMM 'de' yyyy, HH:mm", Mexico);
}
