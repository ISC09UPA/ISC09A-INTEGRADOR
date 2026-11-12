using Microsoft.AspNetCore.Mvc;

namespace InvernaderoInteligente.API.Controllers;

[ApiController]
[Route("api/speech")]
public sealed class SpeechController(
    IWebHostEnvironment environment,
    ILogger<SpeechController> logger) : ControllerBase
{
    private const long MaxAudioBytes = 15 * 1024 * 1024;
    private const long MaxRequestBytes = MaxAudioBytes + 64 * 1024;
    private const int MaxTranscriptionLength = 5000;

    private static readonly HashSet<string> ExtensionesPermitidas =
        new(StringComparer.OrdinalIgnoreCase) { ".aac", ".m4a", ".mp4", ".wav", ".webm" };

    private static readonly HashSet<string> TiposPermitidos =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "audio/aac",
            "audio/mp4",
            "audio/m4a",
            "audio/x-m4a",
            "audio/wav",
            "audio/x-wav",
            "audio/webm",
            "application/octet-stream"
        };

    [HttpPost("receive")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    [ProducesResponseType(typeof(SpeechReceiveResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public ActionResult<SpeechReceiveResponse> Receive(
        [FromForm(Name = "audio")] IFormFile? audio,
        [FromForm(Name = "transcription")] string? transcription)
    {
        if (!environment.IsDevelopment())
            return NotFound();

        if (audio is null || audio.Length == 0)
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Se requiere un archivo de audio no vacío."
            });

        if (audio.Length > MaxAudioBytes)
            return Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                detail: $"El audio no puede superar {MaxAudioBytes / (1024 * 1024)} MB.");

        if (transcription?.Length > MaxTranscriptionLength)
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = $"La transcripción no puede superar {MaxTranscriptionLength} caracteres."
            });

        var extension = Path.GetExtension(audio.FileName);
        if (!ExtensionesPermitidas.Contains(extension) || !TiposPermitidos.Contains(audio.ContentType))
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "El archivo debe ser audio AAC, M4A, MP4, WAV o WebM."
            });

        logger.LogInformation(
            "Received a development Speech upload ({AudioSize} bytes); it was not persisted or processed.",
            audio.Length);

        return Accepted(new SpeechReceiveResponse(
            "received",
            AzureSpeechConfigured: false,
            Persisted: false,
            "Audio y transcripción recibidos. Azure Speech está pendiente; no se guardó ningún dato."));
    }

    public sealed record SpeechReceiveResponse(
        string Status,
        bool AzureSpeechConfigured,
        bool Persisted,
        string Message);
}
