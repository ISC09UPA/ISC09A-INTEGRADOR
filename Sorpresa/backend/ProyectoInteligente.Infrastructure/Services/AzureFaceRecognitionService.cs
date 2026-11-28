
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LockerSmart.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LockerSmart.Infrastructure.Services;

public sealed class AzureFaceRecognitionService : IFaceRecognitionService
{
    private readonly HttpClient _http;
    private readonly string _endpoint;
    private readonly string _key;
    private readonly string _groupId;
    private readonly string _apiVersion;
    private readonly ILogger<AzureFaceRecognitionService> _logger;

    public AzureFaceRecognitionService(
        HttpClient http,
        IConfiguration configuration,
        ILogger<AzureFaceRecognitionService> logger)
    {
        _http = http;
        _logger = logger;

        _endpoint = (configuration["AzureFace:Endpoint"] ??
            throw new InvalidOperationException("Falta AzureFace:Endpoint.")).TrimEnd('/');

        _key = configuration["AzureFace:Key"] ??
            throw new InvalidOperationException("Falta AzureFace:Key.");

        _groupId = configuration["AzureFace:PersonGroupId"] ?? "lockers-users";
        _apiVersion = configuration["AzureFace:ApiVersion"] ?? "v1.0";

        _http.DefaultRequestHeaders.Remove("Ocp-Apim-Subscription-Key");
        _http.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _key);
    }

    public async Task<string> RegisterPersonAsync(
        string displayName,
        string imageSasUrl,
        CancellationToken cancellationToken = default)
    {
        await EnsurePersonGroupAsync(cancellationToken);

        var createPerson = await _http.PostAsJsonAsync(
            $"{_endpoint}/face/{_apiVersion}/persongroups/{_groupId}/persons",
            new { name = displayName },
            cancellationToken);

        await EnsureFaceSuccessAsync(
            createPerson,
            "Crear persona",
            cancellationToken);

        using var personJson = JsonDocument.Parse(
            await createPerson.Content.ReadAsStringAsync(cancellationToken));

        var personId = personJson.RootElement.GetProperty("personId").GetString()
            ?? throw new InvalidOperationException("Azure Face no devolvió personId.");

        var addFace = await _http.PostAsJsonAsync(
            $"{_endpoint}/face/{_apiVersion}/persongroups/{_groupId}/persons/{personId}/persistedFaces?detectionModel=detection_03",
            new { url = imageSasUrl },
            cancellationToken);

        if (!addFace.IsSuccessStatusCode)
        {
            await _http.DeleteAsync(
                $"{_endpoint}/face/{_apiVersion}/persongroups/{_groupId}/persons/{personId}",
                cancellationToken);

            await EnsureFaceSuccessAsync(
                addFace,
                "Agregar rostro",
                cancellationToken);
        }

        var train = await _http.PostAsync(
            $"{_endpoint}/face/{_apiVersion}/persongroups/{_groupId}/train",
            content: null,
            cancellationToken);

        await EnsureFaceSuccessAsync(
            train,
            "Entrenar grupo",
            cancellationToken);

        await WaitForTrainingAsync(cancellationToken);

        return personId;
    }

    public async Task<string?> IdentifyPersonAsync(
        byte[] image,
        CancellationToken cancellationToken = default)
    {
        using var imageContent = new ByteArrayContent(image);

        imageContent.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        // 1. Detectar el rostro.
        var detect = await _http.PostAsync(
            $"{_endpoint}/face/{_apiVersion}/detect?returnFaceId=true&recognitionModel=recognition_04&detectionModel=detection_03",
            imageContent,
            cancellationToken);

        await EnsureFaceSuccessAsync(
            detect,
            "Detectar rostro",
            cancellationToken);

        using var detectJson = JsonDocument.Parse(
            await detect.Content.ReadAsStringAsync(cancellationToken));

        var faces = detectJson.RootElement;

        if (faces.GetArrayLength() != 1)
        {
            _logger.LogWarning(
                "Azure Face detectó {FaceCount} rostros. Se requiere exactamente uno.",
                faces.GetArrayLength());

            return null;
        }

        var faceId = faces[0].GetProperty("faceId").GetString();

        if (string.IsNullOrWhiteSpace(faceId))
            return null;

        // 2. Identificar el rostro dentro del grupo registrado.
        var identify = await _http.PostAsJsonAsync(
            $"{_endpoint}/face/{_apiVersion}/identify",
            new
            {
                faceIds = new[] { faceId },
                personGroupId = _groupId,
                maxNumOfCandidatesReturned = 1,
                confidenceThreshold = 0.6
            },
            cancellationToken);

        await EnsureFaceSuccessAsync(
            identify,
            "Identificar persona",
            cancellationToken);

        using var identifyJson = JsonDocument.Parse(
            await identify.Content.ReadAsStringAsync(cancellationToken));

        var result = identifyJson.RootElement;

        if (result.GetArrayLength() == 0)
            return null;

        var candidates = result[0].GetProperty("candidates");

        if (candidates.GetArrayLength() == 0)
            return null;

        return candidates[0].GetProperty("personId").GetString();
    }

    private async Task EnsurePersonGroupAsync(
        CancellationToken cancellationToken)
    {
        var response = await _http.PutAsJsonAsync(
            $"{_endpoint}/face/{_apiVersion}/persongroups/{_groupId}?recognitionModel=recognition_04",
            new
            {
                name = "Usuarios Locker Inteligente",
                recognitionModel = "recognition_04"
            },
            cancellationToken);

        if (response.IsSuccessStatusCode ||
            response.StatusCode == HttpStatusCode.Conflict)
        {
            return;
        }

        await EnsureFaceSuccessAsync(
            response,
            "Crear grupo de personas",
            cancellationToken);
    }

    private async Task EnsureFaceSuccessAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        _logger.LogError(
            "========== ERROR AZURE FACE ==========\n" +
            "Operación: {Operation}\n" +
            "HTTP: {StatusCode} ({StatusName})\n" +
            "Respuesta: {ResponseBody}\n" +
            "======================================",
            operation,
            (int)response.StatusCode,
            response.StatusCode,
            body);

        throw new InvalidOperationException(
            $"Azure Face falló en '{operation}'. " +
            $"HTTP {(int)response.StatusCode} ({response.StatusCode}). " +
            $"Respuesta: {body}");
    }

    private async Task WaitForTrainingAsync(
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 30; attempt++)
        {
            await Task.Delay(
                TimeSpan.FromSeconds(2),
                cancellationToken);

            var response = await _http.GetAsync(
                $"{_endpoint}/face/{_apiVersion}/persongroups/{_groupId}/training",
                cancellationToken);

            await EnsureFaceSuccessAsync(
                response,
                "Consultar entrenamiento",
                cancellationToken);

            using var json = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(cancellationToken));

            var status = json.RootElement.GetProperty("status").GetString();

            if (status == "succeeded")
                return;

            if (status == "failed")
            {
                throw new InvalidOperationException(
                    "Azure Face no pudo entrenar el grupo de personas.");
            }
        }

        throw new TimeoutException(
            "El entrenamiento de Azure Face sigue en proceso. Intenta iniciar sesión más tarde.");
    }
}
