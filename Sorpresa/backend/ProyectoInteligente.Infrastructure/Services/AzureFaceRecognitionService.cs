using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LockerSmart.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace LockerSmart.Infrastructure.Services;

public sealed class AzureFaceRecognitionService : IFaceRecognitionService
{
    private readonly HttpClient _http;
    private readonly string _endpoint;
    private readonly string _key;
    private readonly string _groupId;
    private readonly string _apiVersion;

    public AzureFaceRecognitionService(HttpClient http, IConfiguration configuration)
    {
        _http = http;
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
        string displayName, string imageSasUrl, CancellationToken cancellationToken = default)
    {
        await EnsurePersonGroupAsync(cancellationToken);

        var createPerson = await _http.PostAsJsonAsync(
            $"{_endpoint}/face/{_apiVersion}/persongroups/{_groupId}/persons",
            new { name = displayName }, cancellationToken);
        createPerson.EnsureSuccessStatusCode();

        using var personJson = JsonDocument.Parse(await createPerson.Content.ReadAsStringAsync(cancellationToken));
        var personId = personJson.RootElement.GetProperty("personId").GetString()
            ?? throw new InvalidOperationException("Azure Face no devolvió personId.");

        var addFace = await _http.PostAsJsonAsync(
            $"{_endpoint}/face/{_apiVersion}/persongroups/{_groupId}/persons/{personId}/persistedFaces?detectionModel=detection_03",
            new { url = imageSasUrl }, cancellationToken);
        if (!addFace.IsSuccessStatusCode)
        {
            await _http.DeleteAsync(
                $"{_endpoint}/face/{_apiVersion}/persongroups/{_groupId}/persons/{personId}", cancellationToken);
            addFace.EnsureSuccessStatusCode();
        }

        var train = await _http.PostAsync(
            $"{_endpoint}/face/{_apiVersion}/persongroups/{_groupId}/train", content: null, cancellationToken);
        train.EnsureSuccessStatusCode();
        await WaitForTrainingAsync(cancellationToken);

        return personId;
    }

    public async Task<string?> IdentifyPersonAsync(byte[] image, CancellationToken cancellationToken = default)
    {
        using var imageContent = new ByteArrayContent(image);
        imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        var detect = await _http.PostAsync(
            $"{_endpoint}/face/{_apiVersion}/detect?returnFaceId=true&recognitionModel=recognition_04&detectionModel=detection_03",
            imageContent, cancellationToken);
        detect.EnsureSuccessStatusCode();

        using var detectJson = JsonDocument.Parse(await detect.Content.ReadAsStringAsync(cancellationToken));
        var faces = detectJson.RootElement;
        if (faces.GetArrayLength() != 1)
            return null; // Exigir exactamente un rostro en la captura.

        var faceId = faces[0].GetProperty("faceId").GetString();
        if (string.IsNullOrWhiteSpace(faceId))
            return null;

        var identify = await _http.PostAsJsonAsync(
            $"{_endpoint}/face/{_apiVersion}/identify",
            new
            {
                faceIds = new[] { faceId },
                personGroupId = _groupId,
                maxNumOfCandidatesReturned = 1,
                confidenceThreshold = 0.6
            }, cancellationToken);
        identify.EnsureSuccessStatusCode();

        using var identifyJson = JsonDocument.Parse(await identify.Content.ReadAsStringAsync(cancellationToken));
        var result = identifyJson.RootElement;
        if (result.GetArrayLength() == 0)
            return null;

        var candidates = result[0].GetProperty("candidates");
        if (candidates.GetArrayLength() == 0)
            return null;

        return candidates[0].GetProperty("personId").GetString();
    }

    private async Task EnsurePersonGroupAsync(CancellationToken cancellationToken)
    {
        var response = await _http.PutAsJsonAsync(
            $"{_endpoint}/face/{_apiVersion}/persongroups/{_groupId}?recognitionModel=recognition_04",
            new { name = "Usuarios Locker Inteligente", recognitionModel = "recognition_04" },
            cancellationToken);

        if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Conflict)
            return;

        response.EnsureSuccessStatusCode();
    }

    private async Task WaitForTrainingAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 30; attempt++)
        {
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            var response = await _http.GetAsync(
                $"{_endpoint}/face/{_apiVersion}/persongroups/{_groupId}/training",
                cancellationToken);
            response.EnsureSuccessStatusCode();

            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var status = json.RootElement.GetProperty("status").GetString();

            if (status == "succeeded") return;
            if (status == "failed")
                throw new InvalidOperationException("Azure Face no pudo entrenar el grupo de personas.");
        }

        throw new TimeoutException("El entrenamiento de Azure Face sigue en proceso. Intenta iniciar sesión más tarde.");
    }
}
