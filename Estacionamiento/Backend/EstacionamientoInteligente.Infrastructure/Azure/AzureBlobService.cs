using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using AirportParking.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace AirportParking.Infrastructure.Azure;

public class AzureBlobService : IAzureBlobService
{
    private readonly HttpClient _httpClient;
    private readonly AzureSettings _settings;
    private readonly ILogger<AzureBlobService> _logger;

    public AzureBlobService(
        HttpClient httpClient,
        AzureSettings settings,
        ILogger<AzureBlobService> logger)
    {
        _httpClient = httpClient;
        _settings = settings;
        _logger = logger;
    }

    public async Task<string?> UploadEvidenceAsync(string blobName, byte[] data, string contentType = "image/jpeg", CancellationToken cancellationToken = default)
    {
        var (account, key, container) = GetStorageConfig();

        if (string.IsNullOrEmpty(account) || string.IsNullOrEmpty(key))
        {
            _logger.LogWarning("Azure Blob Storage credentials missing. Saving evidence locally.");
            return SaveLocalFallback(blobName, data);
        }

        try
        {
            var dateStr = DateTimeOffset.UtcNow.ToString("r");
            var url = $"https://{account}.blob.core.windows.net/{container}/{blobName}";

            var canonicalizedHeaders = $"x-ms-blob-type:BlockBlob\nx-ms-date:{dateStr}\nx-ms-version:2020-10-02";
            var canonicalizedResource = $"/{account}/{container}/{blobName}";

            var stringToSign = $"PUT\n\n\n{data.Length}\n\n{contentType}\n\n\n\n\n\n\n{canonicalizedHeaders}\n{canonicalizedResource}";

            var keyBytes = Convert.FromBase64String(key);
            using var hmac = new HMACSHA256(keyBytes);
            var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));

            using var request = new HttpRequestMessage(HttpMethod.Put, url);
            request.Headers.TryAddWithoutValidation("x-ms-blob-type", "BlockBlob");
            request.Headers.TryAddWithoutValidation("x-ms-date", dateStr);
            request.Headers.TryAddWithoutValidation("x-ms-version", "2020-10-02");
            request.Headers.TryAddWithoutValidation("Authorization", $"SharedKey {account}:{signature}");
            request.Content = new ByteArrayContent(data);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Created)
            {
                _logger.LogInformation("📸 [AZURE BLOB STORAGE]: Evidencia '{BlobName}' subida exitosamente.", blobName);
                return url;
            }

            _logger.LogWarning("Azure Blob upload returned status {StatusCode}. Fallback to local storage.", response.StatusCode);
            return SaveLocalFallback(blobName, data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload blob to Azure Storage, saving locally.");
            return SaveLocalFallback(blobName, data);
        }
    }

    public Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        var (account, key, _) = GetStorageConfig();
        var isConfigured = !string.IsNullOrEmpty(account) && !string.IsNullOrEmpty(key);
        return Task.FromResult(isConfigured);
    }

    private string SaveLocalFallback(string blobName, byte[] data)
    {
        try
        {
            var folder = Path.Combine(AppContext.BaseDirectory, "evidence_storage");
            Directory.CreateDirectory(folder);
            var filePath = Path.Combine(folder, blobName);
            File.WriteAllBytes(filePath, data);
            _logger.LogInformation("📁 Evidencia guardada localmente en {FilePath}", filePath);
            return filePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not save local evidence fallback");
            return $"local://{blobName}";
        }
    }

    private (string Account, string Key, string Container) GetStorageConfig()
    {
        var account = _settings.BlobStorage.AccountName;
        var key = _settings.BlobStorage.AccountKey;
        var container = string.IsNullOrWhiteSpace(_settings.BlobStorage.ContainerName) ? "lpr-evidencias" : _settings.BlobStorage.ContainerName;

        if (string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(_settings.BlobStorage.ConnectionString))
        {
            var parts = _settings.BlobStorage.ConnectionString.Split(';');
            foreach (var part in parts)
            {
                if (part.StartsWith("AccountName=", StringComparison.OrdinalIgnoreCase))
                {
                    account = part.Substring("AccountName=".Length);
                }
                else if (part.StartsWith("AccountKey=", StringComparison.OrdinalIgnoreCase))
                {
                    key = part.Substring("AccountKey=".Length);
                }
            }
        }

        return (account, key, container);
    }
}
