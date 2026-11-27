using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AlmacenInteligente.Application.Iot;
using AlmacenInteligente.Domain.Common;
using AlmacenInteligente.Domain.Iot;
using Microsoft.Extensions.Logging;

namespace AlmacenInteligente.Infrastructure.Iot;

/// <summary>Sección "CustomVision" de la configuración.</summary>
public sealed class OpcionesCustomVision
{
    public string? EndpointEntrenamiento { get; init; }
    public string? ClaveEntrenamiento { get; init; }
    public string? EndpointPrediccion { get; init; }
    public string? ClavePrediccion { get; init; }
    public string? ProyectoId { get; init; }

    /// <summary>Id de recurso ARM del recurso de predicción (/subscriptions/.../accounts/cv-iot-almacen-pred).</summary>
    public string? RecursoPrediccionId { get; init; }
    public string NombrePublicacion { get; init; } = "iot-usuarios";
    public double EsperaEntrenamientoMinutos { get; init; } = 10;

    public bool Completa =>
        new[] { EndpointEntrenamiento, ClaveEntrenamiento, EndpointPrediccion, ClavePrediccion, ProyectoId, RecursoPrediccionId }
            .All(v => !string.IsNullOrWhiteSpace(v));
}

/// <summary>
/// Adaptador de Azure Custom Vision por su API REST (training v3.3, prediction v3.0), con HttpClient para no depender
/// de los SDK, que no tienen versión para .NET 10. Proyecto de clasificación multiclase: una etiqueta por usuario.
/// </summary>
internal sealed class ReconocimientoCustomVision(HttpClient http, OpcionesCustomVision o, ILogger<ReconocimientoCustomVision> log)
    : IReconocimientoFacial
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public bool Configurado => o.Completa;

    private string Proyecto => $"{o.EndpointEntrenamiento!.TrimEnd('/')}/customvision/v3.3/training/projects/{o.ProyectoId}";

    public async Task<string> SubirImagenAsync(string etiqueta, byte[] imagen, CancellationToken ct = default)
    {
        AsegurarConfiguracion();
        var tag = await BuscarEtiquetaAsync(etiqueta, ct)
            ?? await EnviarAsync<EtiquetaCv>(HttpMethod.Post, $"{Proyecto}/tags?name={Uri.EscapeDataString(etiqueta)}", null, ct);

        var resumen = await EnviarAsync<ResumenImagenesCv>(HttpMethod.Post, $"{Proyecto}/images?tagIds={tag.Id}", Binario(imagen), ct);
        var resultado = resumen.Images.FirstOrDefault();
        if (resultado is not { Status: "OK" or "OKDuplicate", Image: not null })
            throw new ImagenInvalidaException($"Custom Vision rechazó la foto ({resultado?.Status ?? "sin respuesta"}).");
        return resultado.Image.Id.ToString();
    }

    public async Task EliminarImagenesAsync(IReadOnlyCollection<string> imagenIds, CancellationToken ct = default)
    {
        AsegurarConfiguracion();
        foreach (var lote in imagenIds.Chunk(256))
            await EnviarAsync(HttpMethod.Delete, $"{Proyecto}/images?imageIds={string.Join(',', lote)}", null, ct);
    }

    public async Task EliminarEtiquetaAsync(string etiqueta, CancellationToken ct = default)
    {
        AsegurarConfiguracion();
        if (await BuscarEtiquetaAsync(etiqueta, ct) is { } tag)
            await EnviarAsync(HttpMethod.Delete, $"{Proyecto}/tags/{tag.Id}", null, ct);
    }

    public async Task<IReadOnlyDictionary<string, int>> ContarImagenesPorEtiquetaAsync(CancellationToken ct = default)
    {
        AsegurarConfiguracion();
        return (await EnviarAsync<List<EtiquetaCv>>(HttpMethod.Get, $"{Proyecto}/tags", null, ct))
            .ToDictionary(t => t.Name, t => t.ImageCount, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<string> EntrenarYPublicarAsync(CancellationToken ct = default)
    {
        AsegurarConfiguracion();
        IteracionCv iteracion;
        try
        {
            iteracion = await EnviarAsync<IteracionCv>(HttpMethod.Post, $"{Proyecto}/train", null, ct);
            log.LogInformation("Entrenamiento de Custom Vision iniciado: iteración {Iteracion}.", iteracion.Name);
        }
        catch (ErrorCustomVision ex) when (ex.Codigo == "BadRequestTrainingNotNeeded")
        {
            // Nada cambió desde el último entrenamiento: se publica (si hace falta) la última iteración completada.
            iteracion = (await IteracionesAsync(ct)).Where(i => i.Status == "Completed").MaxBy(i => i.Created)
                ?? throw new DomainException("Custom Vision dice que no hay cambios para entrenar y no existe ninguna iteración completada.");
        }

        var limite = DateTimeOffset.UtcNow.AddMinutes(o.EsperaEntrenamientoMinutos);
        while (iteracion.Status is "Training" or "Queued")
        {
            if (DateTimeOffset.UtcNow > limite)
                throw new DomainException(
                    $"El entrenamiento sigue en curso después de {o.EsperaEntrenamientoMinutos} minutos. Vuelve a dar \"Entrenar modelo\" en unos minutos para publicarlo.");
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            iteracion = await EnviarAsync<IteracionCv>(HttpMethod.Get, $"{Proyecto}/iterations/{iteracion.Id}", null, ct);
        }
        if (iteracion.Status != "Completed")
            throw new DomainException($"El entrenamiento terminó con estado {iteracion.Status}.");

        // Los nombres de publicación son únicos: primero se despublica la iteración que tenga ese nombre.
        foreach (var anterior in (await IteracionesAsync(ct)).Where(i => i.PublishName == o.NombrePublicacion && i.Id != iteracion.Id))
            await EnviarAsync(HttpMethod.Delete, $"{Proyecto}/iterations/{anterior.Id}/publish", null, ct);
        if (iteracion.PublishName != o.NombrePublicacion)
            await EnviarAsync(HttpMethod.Post,
                $"{Proyecto}/iterations/{iteracion.Id}/publish?publishName={Uri.EscapeDataString(o.NombrePublicacion)}&predictionId={Uri.EscapeDataString(o.RecursoPrediccionId!)}",
                null, ct);

        log.LogInformation("Iteración {Iteracion} publicada como {Publicacion}.", iteracion.Name, o.NombrePublicacion);
        return iteracion.Name;
    }

    public async Task<IReadOnlyList<PrediccionRostro>> ClasificarAsync(byte[] imagen, CancellationToken ct = default)
    {
        if (!Configurado)
            throw new ReconocimientoNoDisponibleException("Reconocimiento facial no configurado");

        var url = $"{o.EndpointPrediccion!.TrimEnd('/')}/customvision/v3.0/Prediction/{o.ProyectoId}/classify/iterations/{Uri.EscapeDataString(o.NombrePublicacion)}/image";
        using var peticion = new HttpRequestMessage(HttpMethod.Post, url) { Content = Binario(imagen) };
        peticion.Headers.Add("Prediction-Key", o.ClavePrediccion);
        using var respuesta = await http.SendAsync(peticion, ct);
        if (!respuesta.IsSuccessStatusCode)
        {
            var error = await LeerErrorAsync(respuesta, ct);
            throw respuesta.StatusCode switch
            {
                HttpStatusCode.NotFound => new ReconocimientoNoDisponibleException("El modelo de reconocimiento no está publicado"),
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new ReconocimientoNoDisponibleException("Clave de predicción de Custom Vision inválida"),
                HttpStatusCode.BadRequest when error.Code?.StartsWith("BadRequestImage", StringComparison.Ordinal) == true =>
                    new ImagenInvalidaException(error.Message ?? error.Code),
                HttpStatusCode.BadRequest => new ReconocimientoNoDisponibleException($"Custom Vision: {error.Message ?? error.Code}"),
                // 429 y 5xx: transitorio, el worker reintenta el mensaje.
                _ => new HttpRequestException($"Custom Vision respondió {(int)respuesta.StatusCode}: {error.Message}", null, respuesta.StatusCode),
            };
        }
        var resultado = await respuesta.Content.ReadFromJsonAsync<PrediccionesCv>(Json, ct);
        return (resultado?.Predictions ?? []).Select(p => new PrediccionRostro(p.TagName, p.Probability)).ToList();
    }

    private async Task<EtiquetaCv?> BuscarEtiquetaAsync(string etiqueta, CancellationToken ct) =>
        (await EnviarAsync<List<EtiquetaCv>>(HttpMethod.Get, $"{Proyecto}/tags", null, ct))
            .FirstOrDefault(t => string.Equals(t.Name, etiqueta, StringComparison.OrdinalIgnoreCase));

    private Task<List<IteracionCv>> IteracionesAsync(CancellationToken ct) =>
        EnviarAsync<List<IteracionCv>>(HttpMethod.Get, $"{Proyecto}/iterations", null, ct);

    private async Task<T> EnviarAsync<T>(HttpMethod metodo, string url, HttpContent? contenido, CancellationToken ct)
    {
        using var respuesta = await EnviarAsync(metodo, url, contenido, ct);
        return await respuesta.Content.ReadFromJsonAsync<T>(Json, ct)
            ?? throw new DomainException("Custom Vision devolvió una respuesta vacía.");
    }

    private async Task<HttpResponseMessage> EnviarAsync(HttpMethod metodo, string url, HttpContent? contenido, CancellationToken ct)
    {
        using var peticion = new HttpRequestMessage(metodo, url) { Content = contenido };
        peticion.Headers.Add("Training-Key", o.ClaveEntrenamiento);
        var respuesta = await http.SendAsync(peticion, ct);
        if (respuesta.IsSuccessStatusCode)
            return respuesta;

        var estado = respuesta.StatusCode;
        var error = await LeerErrorAsync(respuesta, ct);
        respuesta.Dispose();
        if ((int)estado >= 500 || estado == HttpStatusCode.TooManyRequests)
            throw new HttpRequestException($"Custom Vision respondió {(int)estado}: {error.Message}", null, estado);
        throw new ErrorCustomVision(error.Code, $"Custom Vision: {error.Message ?? error.Code ?? estado.ToString()}");
    }

    private static async Task<ErrorCv> LeerErrorAsync(HttpResponseMessage respuesta, CancellationToken ct)
    {
        try
        {
            return await respuesta.Content.ReadFromJsonAsync<ErrorCv>(Json, ct) ?? new ErrorCv(null, null);
        }
        catch (JsonException)
        {
            return new ErrorCv(null, respuesta.ReasonPhrase);
        }
    }

    private static ByteArrayContent Binario(byte[] imagen)
    {
        var contenido = new ByteArrayContent(imagen);
        contenido.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return contenido;
    }

    private void AsegurarConfiguracion()
    {
        if (!Configurado)
            throw new ReconocimientoNoDisponibleException("Custom Vision no está configurado (sección CustomVision).");
    }

    private sealed class ErrorCustomVision(string? codigo, string mensaje) : DomainException(mensaje)
    {
        public string? Codigo { get; } = codigo;
    }

    private sealed record ErrorCv(string? Code, string? Message);
    private sealed record EtiquetaCv(Guid Id, string Name, int ImageCount);
    private sealed record IteracionCv(Guid Id, string Name, string Status, string? PublishName, DateTimeOffset Created);
    private sealed record ResumenImagenesCv(bool IsBatchSuccessful, List<ResultadoImagenCv> Images);
    private sealed record ResultadoImagenCv(string Status, ImagenCv? Image);
    private sealed record ImagenCv(Guid Id);
    private sealed record PrediccionesCv(List<PrediccionCv> Predictions);
    private sealed record PrediccionCv(double Probability, Guid TagId, string TagName);
}
