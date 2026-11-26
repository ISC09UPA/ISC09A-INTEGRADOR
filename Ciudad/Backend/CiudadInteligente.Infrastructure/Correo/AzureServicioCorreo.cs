using System.Net;
using Azure;
using Azure.Communication.Email;
using CiudadInteligente.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace CiudadInteligente.Infrastructure.Correo;

/// <summary>
/// Configuracion de correo ("Correo" en appsettings):
///   Habilitado        (appsettings.json)
///   Remitente         (appsettings.json)  DoNotReply@xxxx.azurecomm.net (dominio de Azure)
///   Destinatarios     (appsettings.json)  correos separados por coma
///   ConnectionString  (dotnet user-secrets / variable de entorno Correo__ConnectionString, NO en el repo)
/// </summary>
public sealed record OpcionesCorreo(bool Habilitado, string ConnectionString, string Remitente, IReadOnlyList<string> Destinatarios)
{
    public bool EstaListo => Habilitado
        && !string.IsNullOrWhiteSpace(ConnectionString)
        && !string.IsNullOrWhiteSpace(Remitente)
        && Destinatarios.Count > 0;
}

public sealed class AzureServicioCorreo(OpcionesCorreo opciones, ILogger<AzureServicioCorreo> logger) : IServicioCorreo
{
    private readonly EmailClient _cliente = new(opciones.ConnectionString);

    public async Task NotificarAsync(string asunto, string mensaje, CancellationToken cancelacion = default)
    {
        try
        {
            var contenido = new EmailContent(asunto)
            {
                PlainText = mensaje,
                Html = $"<p>{WebUtility.HtmlEncode(mensaje).Replace("\n", "<br>")}</p>"
            };
            var correo = new EmailMessage(
                opciones.Remitente,
                new EmailRecipients(opciones.Destinatarios.Select(d => new EmailAddress(d))),
                contenido);

            // Started: Azure lo acepto; la entrega sigue por su cuenta.
            await _cliente.SendAsync(WaitUntil.Started, correo, cancelacion);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo enviar el correo \"{Asunto}\"", asunto);
        }
    }
}

/// <summary>Correo apagado o sin configurar: no hace nada.</summary>
public sealed class ServicioCorreoNulo : IServicioCorreo
{
    public Task NotificarAsync(string asunto, string mensaje, CancellationToken cancelacion = default) => Task.CompletedTask;
}
