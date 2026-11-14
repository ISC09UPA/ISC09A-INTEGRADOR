using CasaDomotica.Home.Domain.Interfaces;
using CasaDomotica.Home.Infrastructure.Runtime;
using SkiaSharp;
using Xunit;

namespace CasaDomotica.Home.Application.Tests;

public sealed class CaptureImageValidatorTests
{
    private static byte[] Image(SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(16, 16);
        bitmap.Erase(SKColors.CornflowerBlue);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(format, 90);
        return encoded.ToArray();
    }
    [Theory]
    [InlineData(SKEncodedImageFormat.Jpeg, "image/jpeg", "jpg")]
    [InlineData(SKEncodedImageFormat.Png, "image/png", "png")]
    public void AcceptsOnlyFullyDecodableSupportedImageBytes(SKEncodedImageFormat format, string contentType, string extension)
    {
        Assert.Equal(extension, CaptureImageValidator.Validate(Image(format), contentType));
    }
    [Fact]
    public void RejectsMismatchedTruncatedAndOversizedImages()
    {
        var image = Image(SKEncodedImageFormat.Png);
        Assert.Throws<HomeException>(() => CaptureImageValidator.Validate(image, "image/jpeg"));
        Assert.Throws<HomeException>(() => CaptureImageValidator.Validate(image[..(image.Length / 2)], "image/png"));
        Assert.Throws<HomeException>(() => CaptureImageValidator.Validate(new byte[CaptureImageValidator.MaximumBytes + 1], "image/png"));
        Assert.Throws<HomeException>(() => CaptureImageValidator.Validate([1, 2, 3], "image/png"));
    }
}
