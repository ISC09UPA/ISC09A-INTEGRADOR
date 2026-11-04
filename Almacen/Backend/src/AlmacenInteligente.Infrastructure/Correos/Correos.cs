using System.Net;
using System.Net.Mail;
using System.Text;
using AlmacenInteligente.Application.Abstracciones;
using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Logging;

namespace AlmacenInteligente.Infrastructure.Correos;

/// <summary>Sección "Correo" de appsettings. Se elige Azure si hay cadena de conexión, si no SMTP si hay host, si no solo log.</summary>
internal sealed record OpcionesCorreo(
    string? Remitente, string NombreRemitente, string? CadenaAzure,
    string? SmtpHost, int SmtpPuerto, string? SmtpUsuario, string? SmtpContrasena, bool SmtpSsl);

/// <summary>Azure Communication Services Email. El remitente debe pertenecer a un dominio conectado al recurso de ACS.</summary>
internal sealed class CorreoAzure(EmailClient cliente, OpcionesCorreo opciones, ILogger<CorreoAzure> log) : IEmailSender
{
    public async Task EnviarAsync(MensajeCorreo mensaje, CancellationToken ct = default)
    {
        var destinatarios = new EmailRecipients(mensaje.Para.Select(p => new EmailAddress(p)).ToList());
        var correo = new EmailMessage(opciones.Remitente, destinatarios, new EmailContent(mensaje.Asunto) { Html = mensaje.CuerpoHtml });
        foreach (var a in mensaje.Adjuntos ?? [])
            correo.Attachments.Add(new EmailAttachment(a.NombreArchivo, a.TipoContenido, BinaryData.FromBytes(a.Contenido)));

        var operacion = await cliente.SendAsync(WaitUntil.Completed, correo, ct);
        if (operacion.Value.Status != EmailSendStatus.Succeeded)
            throw new InvalidOperationException($"Azure Communication Services no envió el correo \"{mensaje.Asunto}\" (estado {operacion.Value.Status}).");
        log.LogInformation("Correo \"{Asunto}\" enviado a {Para} (ACS {Id}).", mensaje.Asunto, string.Join(", ", mensaje.Para), operacion.Id);
    }
}

/// <summary>SMTP genérico. En desarrollo apunta a Mailpit (localhost:1025) para ver los correos sin enviarlos de verdad.</summary>
internal sealed class CorreoSmtp(OpcionesCorreo opciones, ILogger<CorreoSmtp> log) : IEmailSender
{
    public async Task EnviarAsync(MensajeCorreo mensaje, CancellationToken ct = default)
    {
        using var cliente = new SmtpClient(opciones.SmtpHost, opciones.SmtpPuerto) { EnableSsl = opciones.SmtpSsl };
        if (!string.IsNullOrEmpty(opciones.SmtpUsuario))
            cliente.Credentials = new NetworkCredential(opciones.SmtpUsuario, opciones.SmtpContrasena);

        using var correo = new MailMessage
        {
            From = new MailAddress(opciones.Remitente ?? "no-responder@almacen.local", opciones.NombreRemitente),
            Subject = mensaje.Asunto,
            SubjectEncoding = Encoding.UTF8,
            Body = mensaje.CuerpoHtml,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = true,
        };
        foreach (var para in mensaje.Para)
            correo.To.Add(para);
        foreach (var a in mensaje.Adjuntos ?? [])
            correo.Attachments.Add(new Attachment(new MemoryStream(a.Contenido), a.NombreArchivo, a.TipoContenido));

        await cliente.SendMailAsync(correo, ct);
        log.LogInformation("Correo \"{Asunto}\" enviado por SMTP a {Para}.", mensaje.Asunto, string.Join(", ", mensaje.Para));
    }
}

/// <summary>Sin servicio de correo configurado: solo deja constancia en el log para no frenar el resto del sistema.</summary>
internal sealed class CorreoEnLog(ILogger<CorreoEnLog> log) : IEmailSender
{
    public Task EnviarAsync(MensajeCorreo mensaje, CancellationToken ct = default)
    {
        log.LogWarning("No hay servicio de correo configurado (Correo:Azure:CadenaConexion o Correo:Smtp:Host). " +
            "No se envió \"{Asunto}\" a {Para}.", mensaje.Asunto, string.Join(", ", mensaje.Para));
        return Task.CompletedTask;
    }
}
