using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CasaDomotica.Security.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace CasaDomotica.Security.Infrastructure;

public sealed class AzureFaceBiometricProvider(HttpClient client, IConfiguration configuration) : IFaceBiometricProvider
{
    public async Task<FaceCapabilityResult> CheckAccessAsync(CancellationToken ct)
    {
        var endpoint = configuration["Face:Endpoint"]?.Trim().TrimEnd('/');
        var apiKey = configuration["Face:ApiKey"]?.Trim();
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var baseUri) || baseUri.Scheme != Uri.UriSchemeHttps || string.IsNullOrWhiteSpace(apiKey))
            return new(false, "faceNotConfigured", "El recurso Azure Face no está configurado en el servicio.");

        // Microsoft requires explicit Limited Access approval for Face Identify/Verify.
        // Without confirmed approval, facial enrollment/verification stays blocked as an external dependency.
        var approved = string.Equals(configuration["Face:IdentifyVerifyApproved"], "true", StringComparison.OrdinalIgnoreCase);
        if (!approved)
            return new(false, "faceAccessPendingApproval", "Azure Face Identify/Verify requiere aprobación explícita de acceso limitado de Microsoft antes de habilitarse.");

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{endpoint}/face/v1.0/persongroups");
            req.Headers.Add("Ocp-Apim-Subscription-Key", apiKey);
            using var res = await client.SendAsync(req, ct);
            if (res.IsSuccessStatusCode)
                return new(true, "ready", "Azure Face disponible.");
            if (res.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return new(false, "faceAccessPendingApproval", "Azure Face rechazó la operación por permisos o acceso limitado pendiente.");
            return new(false, "faceProviderUnavailable", $"Azure Face respondió HTTP {(int)res.StatusCode}.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new(false, "faceProviderUnavailable", "No se pudo verificar disponibilidad de Azure Face.");
        }
    }

    public async Task<FaceEnrollResult> EnrollFromImageAsync(Guid houseId, Guid personId, byte[] imageBytes, string contentType, CancellationToken ct)
    {
        var cap = await CheckAccessAsync(ct);
        if (!cap.Available) return new(false, null, null, cap.Code, cap.Detail);

        var endpoint = configuration["Face:Endpoint"]!.Trim().TrimEnd('/');
        var apiKey = configuration["Face:ApiKey"]!.Trim();
        var groupId = $"house-{houseId:N}";

        try
        {
            await EnsurePersonGroupAsync(endpoint, apiKey, groupId, ct);
            using var createPerson = new HttpRequestMessage(HttpMethod.Post, $"{endpoint}/face/v1.0/persongroups/{groupId}/persons");
            createPerson.Headers.Add("Ocp-Apim-Subscription-Key", apiKey);
            createPerson.Content = new StringContent(JsonSerializer.Serialize(new { name = personId.ToString("N") }), Encoding.UTF8, "application/json");
            using var createRes = await client.SendAsync(createPerson, ct);
            if (!createRes.IsSuccessStatusCode)
                return new(false, null, null, MapError(createRes.StatusCode), "No se pudo crear la referencia facial en Azure Face.");

            using var personDoc = JsonDocument.Parse(await createRes.Content.ReadAsStringAsync(ct));
            var facePersonId = personDoc.RootElement.GetProperty("personId").GetString();
            if (string.IsNullOrWhiteSpace(facePersonId))
                return new(false, null, null, "faceProviderFailed", "Azure Face no devolvió identificador de persona.");

            using var addFace = new HttpRequestMessage(HttpMethod.Post, $"{endpoint}/face/v1.0/persongroups/{groupId}/persons/{Uri.EscapeDataString(facePersonId)}/persistedfaces");
            addFace.Headers.Add("Ocp-Apim-Subscription-Key", apiKey);
            addFace.Content = new ByteArrayContent(imageBytes);
            addFace.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            using var faceRes = await client.SendAsync(addFace, ct);
            if (!faceRes.IsSuccessStatusCode)
            {
                await DeletePersonAsync(houseId, facePersonId, CancellationToken.None);
                return new(false, null, null, MapError(faceRes.StatusCode), "Azure Face no pudo registrar el rostro de la captura.");
            }

            return new(true, facePersonId, 0.90, null, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new(false, null, null, "faceProviderFailed", "Error al comunicarse con Azure Face.");
        }
    }

    public async Task<FaceVerifyResult> VerifyImageAsync(Guid houseId, string referenceId, byte[] imageBytes, string contentType, CancellationToken ct)
    {
        var cap = await CheckAccessAsync(ct);
        if (!cap.Available) return new(false, null, cap.Code, cap.Detail);

        var endpoint = configuration["Face:Endpoint"]!.Trim().TrimEnd('/');
        var apiKey = configuration["Face:ApiKey"]!.Trim();
        var groupId = $"house-{houseId:N}";

        try
        {
            using var detect = new HttpRequestMessage(HttpMethod.Post, $"{endpoint}/face/v1.0/detect");
            detect.Headers.Add("Ocp-Apim-Subscription-Key", apiKey);
            detect.Content = new ByteArrayContent(imageBytes);
            detect.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            using var detectRes = await client.SendAsync(detect, ct);
            if (!detectRes.IsSuccessStatusCode)
                return new(false, null, MapError(detectRes.StatusCode), "No se detectó rostro válido.");

            using var faces = JsonDocument.Parse(await detectRes.Content.ReadAsStringAsync(ct));
            if (faces.RootElement.ValueKind != JsonValueKind.Array || faces.RootElement.GetArrayLength() != 1)
                return new(false, null, "faceNotDetected", "La captura debe contener exactamente un rostro.");

            var faceId = faces.RootElement[0].GetProperty("faceId").GetString();
            using var verify = new HttpRequestMessage(HttpMethod.Post, $"{endpoint}/face/v1.0/verify");
            verify.Headers.Add("Ocp-Apim-Subscription-Key", apiKey);
            verify.Content = new StringContent(JsonSerializer.Serialize(new { faceId, personGroupId = groupId, personId = referenceId }), Encoding.UTF8, "application/json");
            using var verifyRes = await client.SendAsync(verify, ct);
            if (!verifyRes.IsSuccessStatusCode)
                return new(false, null, MapError(verifyRes.StatusCode), "Azure Face rechazó la verificación.");

            using var doc = JsonDocument.Parse(await verifyRes.Content.ReadAsStringAsync(ct));
            var identical = doc.RootElement.TryGetProperty("isIdentical", out var idProp) && idProp.GetBoolean();
            var confidence = doc.RootElement.TryGetProperty("confidence", out var confProp) ? confProp.GetDouble() : 0.0;
            return new(identical, confidence, identical ? null : "faceMismatch", identical ? null : "El rostro no coincide.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new(false, null, "faceProviderFailed", "Error al verificar con Azure Face.");
        }
    }

    public async Task DeletePersonAsync(Guid houseId, string referenceId, CancellationToken ct)
    {
        var endpoint = configuration["Face:Endpoint"]?.Trim().TrimEnd('/');
        var apiKey = configuration["Face:ApiKey"]?.Trim();
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(referenceId))
            return;

        try
        {
            var groupId = $"house-{houseId:N}";
            using var req = new HttpRequestMessage(HttpMethod.Delete, $"{endpoint}/face/v1.0/persongroups/{groupId}/persons/{Uri.EscapeDataString(referenceId)}");
            req.Headers.Add("Ocp-Apim-Subscription-Key", apiKey);
            await client.SendAsync(req, ct);
        }
        catch
        {
            // Deletion failures must not prevent local revocation or retention purges.
        }
    }

    private async Task EnsurePersonGroupAsync(string endpoint, string apiKey, string groupId, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Put, $"{endpoint}/face/v1.0/persongroups/{groupId}");
        req.Headers.Add("Ocp-Apim-Subscription-Key", apiKey);
        req.Content = new StringContent(JsonSerializer.Serialize(new { name = groupId }), Encoding.UTF8, "application/json");
        await client.SendAsync(req, ct);
    }

    private static string MapError(HttpStatusCode status) =>
        status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden ? "faceAccessPendingApproval" : "faceProviderFailed";
}

public sealed class BiometricRetentionWorker(IServiceScopeFactory scopes, TimeProvider clock) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var store = scope.ServiceProvider.GetRequiredService<IBiometricStore>();
                var face = scope.ServiceProvider.GetRequiredService<IFaceBiometricProvider>();
                var purged = await store.PurgeExpiredAsync(clock.GetUtcNow(), stoppingToken);
                foreach (var (houseId, refId) in purged)
                    await face.DeletePersonAsync(houseId, refId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch { }

            try { await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}