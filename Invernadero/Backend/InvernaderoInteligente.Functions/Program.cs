using InvernaderoInteligente.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

// Cliente HTTP hacia la API de Telemetría.
//   TelemetriaApi__BaseUrl  (obligatorio)  ej. http://localhost:5010
//   TelemetriaApi__ApiKey   (opcional)     se manda en el header X-Api-Key
builder.Services.AddHttpClient<ITelemetriaApiClient, TelemetriaApiClient>((sp, http) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var baseUrl = config["TelemetriaApi:BaseUrl"]
        ?? throw new InvalidOperationException("Falta la configuración TelemetriaApi__BaseUrl.");

    http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    http.Timeout = TimeSpan.FromSeconds(30);

    var apiKey = config["TelemetriaApi:ApiKey"];
    if (!string.IsNullOrWhiteSpace(apiKey))
        http.DefaultRequestHeaders.Add(TelemetriaApiClient.HeaderApiKey, apiKey);
});

builder.Build().Run();
