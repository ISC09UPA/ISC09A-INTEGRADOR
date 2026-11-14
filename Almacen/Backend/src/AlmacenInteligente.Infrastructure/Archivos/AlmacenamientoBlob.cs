using AlmacenInteligente.Application.Abstracciones;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace AlmacenInteligente.Infrastructure.Archivos;

/// <summary>
/// Imágenes en Azure Blob Storage (o Azurite en desarrollo), en un contenedor privado. Nunca se exponen URLs del
/// blob: la API y la web las sirven por /archivos/... para seguir exigiendo sesión. Las rutas son "productos/{id}.jpg",
/// el mismo formato que usaba la versión en disco, así las rutas ya guardadas en la base siguen sirviendo.
/// </summary>
internal sealed class AlmacenamientoBlob(BlobContainerClient contenedor) : IFileStorage
{
    private static readonly Dictionary<string, string> Extensiones = new()
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp",
    };

    private volatile bool _contenedorListo;

    public async Task<string> GuardarAsync(Stream contenido, string nombreArchivo, string tipoContenido, CancellationToken ct = default)
    {
        await AsegurarContenedorAsync(ct);
        var extension = Extensiones.GetValueOrDefault(tipoContenido) ?? Path.GetExtension(nombreArchivo).ToLowerInvariant();
        var ruta = $"productos/{Guid.CreateVersion7():N}{extension}";
        await contenedor.GetBlobClient(ruta).UploadAsync(contenido, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = tipoContenido },
        }, ct);
        return ruta;
    }

    public async Task<Stream?> AbrirAsync(string ruta, CancellationToken ct = default)
    {
        try
        {
            var descarga = await contenedor.GetBlobClient(Validar(ruta)).DownloadStreamingAsync(cancellationToken: ct);
            return descarga.Value.Content;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public Task EliminarAsync(string ruta, CancellationToken ct = default) =>
        contenedor.GetBlobClient(Validar(ruta)).DeleteIfExistsAsync(cancellationToken: ct);

    private async Task AsegurarContenedorAsync(CancellationToken ct)
    {
        if (_contenedorListo)
            return;
        await contenedor.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);
        _contenedorListo = true;
    }

    /// <summary>Nada de rutas absolutas ni "..": la ruta llega de la URL de /archivos.</summary>
    private static string Validar(string ruta)
    {
        if (string.IsNullOrWhiteSpace(ruta) || ruta.StartsWith('/') || ruta.Contains('\\') || ruta.Split('/').Any(p => p is "" or "." or ".."))
            throw new UnauthorizedAccessException("Ruta de archivo no válida.");
        return ruta;
    }
}
