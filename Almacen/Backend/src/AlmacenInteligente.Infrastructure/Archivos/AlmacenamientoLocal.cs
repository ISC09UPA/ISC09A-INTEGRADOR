using AlmacenInteligente.Application.Abstracciones;
using Microsoft.Extensions.Configuration;

namespace AlmacenInteligente.Infrastructure.Archivos;

/// <summary>
/// Guarda archivos en disco bajo "Archivos:Ruta" (por defecto ./data/archivos). En Azure App Service
/// apunta a /home para que persistan. Las rutas devueltas son relativas y nunca salen de la carpeta raíz.
/// </summary>
internal sealed class AlmacenamientoLocal : IFileStorage
{
    private static readonly Dictionary<string, string> Extensiones = new()
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp",
    };

    private readonly string _raiz;

    public AlmacenamientoLocal(IConfiguration configuracion)
    {
        _raiz = Path.GetFullPath(configuracion["Archivos:Ruta"] ?? Path.Combine(Directory.GetCurrentDirectory(), "data", "archivos"));
        Directory.CreateDirectory(_raiz);
    }

    public async Task<string> GuardarAsync(Stream contenido, string nombreArchivo, string tipoContenido, CancellationToken ct = default)
    {
        var extension = Extensiones.GetValueOrDefault(tipoContenido) ?? Path.GetExtension(nombreArchivo).ToLowerInvariant();
        var relativa = $"productos/{Guid.CreateVersion7():N}{extension}";
        var destino = Resolver(relativa);
        Directory.CreateDirectory(Path.GetDirectoryName(destino)!);
        await using var archivo = File.Create(destino);
        await contenido.CopyToAsync(archivo, ct);
        return relativa;
    }

    public Task<Stream?> AbrirAsync(string ruta, CancellationToken ct = default)
    {
        var completa = Resolver(ruta);
        return Task.FromResult<Stream?>(File.Exists(completa) ? File.OpenRead(completa) : null);
    }

    public Task EliminarAsync(string ruta, CancellationToken ct = default)
    {
        var completa = Resolver(ruta);
        if (File.Exists(completa))
            File.Delete(completa);
        return Task.CompletedTask;
    }

    private string Resolver(string relativa)
    {
        var completa = Path.GetFullPath(Path.Combine(_raiz, relativa));
        if (!completa.StartsWith(_raiz + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Ruta de archivo no válida.");
        return completa;
    }
}
