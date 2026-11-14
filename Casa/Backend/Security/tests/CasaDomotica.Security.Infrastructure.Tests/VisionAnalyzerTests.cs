using System.Net;
using CasaDomotica.Security.Infrastructure;
using Microsoft.Extensions.Configuration;
using Xunit;

public sealed class VisionAnalyzerTests
{
    private sealed class Handler(HttpStatusCode status, string response) : HttpMessageHandler
    {
        public string? Uri; public string? Key; public byte[]? Bytes;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Uri = request.RequestUri!.ToString(); Key = request.Headers.GetValues("Ocp-Apim-Subscription-Key").Single(); Bytes = await request.Content!.ReadAsByteArrayAsync(ct);
            return new HttpResponseMessage(status) { Content = new StringContent(response) };
        }
    }
    private static IConfiguration Config() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Vision:Endpoint"] = "https://vision.example.invalid", ["Vision:ApiKey"] = "isolated-test-key" }).Build();
    [Fact]
    public async Task AnalysisUsesPrivateBytesAndParsesOnlyTheProviderCaption()
    {
        var handler = new Handler(HttpStatusCode.OK, """{"captionResult":{"text":"A test caption.","confidence":0.8}}""");
        using var client = new HttpClient(handler);
        var output = await new AzureVisionAnalyzer(client, Config()).AnalyzeAsync([1, 2, 3], default);
        Assert.Equal("A test caption.", output); Assert.Equal("isolated-test-key", handler.Key); Assert.Equal(new byte[] { 1, 2, 3 }, handler.Bytes);
        Assert.Contains("api-version=2024-02-01", handler.Uri);
    }
    [Fact]
    public async Task ProviderFailuresAreNotPresentedAsAnalysis()
    {
        using var client = new HttpClient(new Handler(HttpStatusCode.ServiceUnavailable, "{}"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new AzureVisionAnalyzer(client, Config()).AnalyzeAsync([1], default));
    }
}
