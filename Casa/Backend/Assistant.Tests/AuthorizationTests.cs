using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class AuthorizationTests
{
    private static readonly Guid House = Guid.Parse("a3108eb1-6b1b-4281-91f7-05097515390f");
    [Fact]
    public async Task NoSessionDoesNotCallServices()
    {
        var handler = new FakeServices();
        using var app = Create(handler); using var client = app.CreateClient();
        var response = await client.PostAsJsonAsync($"/api/v1/assistant/houses/{House}/ask", new { message = "¿Cómo está mi casa?" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode); Assert.Equal(0, handler.Calls);
    }
    [Fact]
    public async Task OtherHouseIsForbiddenWithoutConsultingHomeOrAi()
    {
        var handler = new FakeServices();
        using var app = Create(handler); using var client = app.CreateClient(); client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");
        var response = await client.PostAsJsonAsync($"/api/v1/assistant/houses/{Guid.NewGuid()}/ask", new { message = "Estado" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); Assert.Equal(1, handler.Calls);
    }
    [Fact]
    public async Task MissingHomeReportReturnsHonestAnswerWithoutCallingAi()
    {
        var handler = new FakeServices();
        using var app = Create(handler); using var client = app.CreateClient(); client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");
        var response = await client.PostAsJsonAsync($"/api/v1/assistant/houses/{House}/ask", new { message = "Estado" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Contains("no hay reportes", await response.Content.ReadAsStringAsync()); Assert.Equal(2, handler.Calls);
    }
    private static WebApplicationFactory<Program> Create(FakeServices handler) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["Services:IdentityUrl"] = "https://identity.example", ["Services:HomeUrl"] = "https://home.example", ["OpenAI:Endpoint"] = "https://model.example", ["OpenAI:Deployment"] = "test" }));
        builder.ConfigureServices(services => services.AddHttpClient("dependencies").ConfigurePrimaryHttpMessageHandler(() => handler));
    });
    private sealed class FakeServices : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            if (request.RequestUri!.Host == "identity.example") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { houses = new[] { new { houseId = House } } }) });
            if (request.RequestUri.Host == "home.example") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            throw new InvalidOperationException("Unexpected provider call.");
        }
    }
}
