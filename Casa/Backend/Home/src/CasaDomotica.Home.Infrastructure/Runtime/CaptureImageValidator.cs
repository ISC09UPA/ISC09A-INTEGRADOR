using CasaDomotica.Home.Domain.Interfaces;
using SkiaSharp;

namespace CasaDomotica.Home.Infrastructure.Runtime;

/// <summary>Validates complete, bounded image bytes before any image is made available.</summary>
public static class CaptureImageValidator
{
    public const int MaximumBytes = 5 * 1024 * 1024;
    public static string Validate(byte[] bytes, string contentType)
    {
        if (bytes.Length is <= 0 or > MaximumBytes) throw new HomeException("invalidPayload", "Image size is invalid.");
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        if (codec is null) throw new HomeException("invalidPayload", "Image format is invalid.");
        var extension = (codec.EncodedFormat, contentType) switch
        {
            (SKEncodedImageFormat.Jpeg, "image/jpeg") => "jpg",
            (SKEncodedImageFormat.Png, "image/png") => "png",
            _ => throw new HomeException("invalidPayload", "Image format does not match its content type.")
        };
        var source = codec.Info;
        // Skia reports zero frames for ordinary non-animated still images.
        if (source.Width is <= 0 or > 4096 || source.Height is <= 0 or > 4096 || (long)source.Width * source.Height > 12_000_000 || codec.FrameCount > 1)
            throw new HomeException("invalidPayload", "Image dimensions or frame count are invalid.");
        using var bitmap = new SKBitmap(new SKImageInfo(source.Width, source.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        if (bitmap.GetPixels() == IntPtr.Zero || codec.GetPixels(bitmap.Info, bitmap.GetPixels()) != SKCodecResult.Success)
            throw new HomeException("invalidPayload", "Image content is incomplete or invalid.");
        return extension;
    }
}
