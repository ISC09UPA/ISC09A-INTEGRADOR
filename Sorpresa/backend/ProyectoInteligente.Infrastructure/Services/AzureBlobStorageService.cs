using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using LockerSmart.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace LockerSmart.Infrastructure.Services;

public sealed class AzureBlobStorageService : IBlobStorageService
{
    private readonly BlobContainerClient _container;
    private readonly StorageSharedKeyCredential _credential;
    private readonly string _accountName;

    public AzureBlobStorageService(IConfiguration configuration)
    {
        var connectionString = configuration["AzureBlobStorage:ConnectionString"]
            ?? throw new InvalidOperationException("Falta AzureBlobStorage:ConnectionString.");

        var values = connectionString.Split(';')
            .Where(x => x.Contains('='))
            .Select(x => x.Split('=', 2))
            .ToDictionary(x => x[0], x => x[1], StringComparer.OrdinalIgnoreCase);

        if (!values.TryGetValue("AccountName", out var accountName) ||
            !values.TryGetValue("AccountKey", out var accountKey))
            throw new InvalidOperationException("La cadena de Blob Storage debe incluir AccountName y AccountKey.");

        _accountName = accountName;
        _credential = new StorageSharedKeyCredential(_accountName, accountKey);
        var serviceClient = new BlobServiceClient(connectionString);
        var containerName = configuration["AzureBlobStorage:ContainerName"] ?? "fotos-usuarios";
        _container = serviceClient.GetBlobContainerClient(containerName);
        _container.CreateIfNotExists(PublicAccessType.None);
    }

    public async Task<string> UploadAsync(
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (content is null || !content.CanRead)
            throw new ArgumentException("El contenido de la imagen no es válido.");

        var extension = contentType switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            _ => throw new ArgumentException("Solo se permiten imágenes JPG o PNG.")
        };

        var blobName = $"usuarios/{Guid.NewGuid():N}{extension}";
        var blob = _container.GetBlobClient(blobName);

        await blob.UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = contentType
            }
        }, cancellationToken);

        return blobName;
    }

    public async Task<string> CreateReadSasUrlAsync(
        string blobName,
        TimeSpan validity,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(blobName) ||
            !blobName.StartsWith("usuarios/", StringComparison.Ordinal) ||
            blobName.Contains(".."))
            throw new ArgumentException("Referencia de fotografía no válida.");

        if (validity <= TimeSpan.Zero || validity > TimeSpan.FromMinutes(10))
            throw new ArgumentOutOfRangeException(
                nameof(validity), "La vigencia máxima permitida es de 10 minutos.");

        var blob = _container.GetBlobClient(blobName);

        if (!await blob.ExistsAsync(cancellationToken))
            throw new FileNotFoundException("La fotografía no existe en Blob Storage.");

        var builder = new BlobSasBuilder
        {
            BlobContainerName = _container.Name,
            BlobName = blobName,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.Add(validity)
        };

        builder.SetPermissions(BlobSasPermissions.Read);

        return blob.GenerateSasUri(builder).ToString();
    }

    public async Task DeleteAsync(
        string blobName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(blobName) ||
            !blobName.StartsWith("usuarios/", StringComparison.Ordinal) ||
            blobName.Contains(".."))
            throw new ArgumentException("Referencia de fotografía no válida.");

        var blob = _container.GetBlobClient(blobName);
        await blob.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }
}
