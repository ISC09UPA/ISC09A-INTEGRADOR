using System.Net.Http.Headers;
using System.Threading.RateLimiting;
using Azure.Core;
using Azure.Identity;
using CasaDomotica.Assistant.Api;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient("dependencies", client => client.Timeout = TimeSpan.FromSeconds(45));
builder.Services.AddSingleton<TokenCredential, DefaultAzureCredential>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<AssistantOperations>();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    if (!context.HttpContext.Request.Path.StartsWithSegments("/api/v1")) return;
    var status = context.ProblemDetails.Status ?? 500;
    var code = status switch
    {
        400 or 422 => "badRequest",
        401 => "invalidCredential",
        403 => "forbidden",
        404 => "notFound",
        429 => "rateLimited",
        _ => "serviceUnavailable"
    };
    context.ProblemDetails.Extensions.Clear();
    context.ProblemDetails.Extensions["code"] = code;
    context.ProblemDetails.Extensions["correlationId"] = context.HttpContext.TraceIdentifier;
});
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    var origins = builder.Configuration.GetSection("Cors:Origins").GetChildren().Select(x => x.Value).OfType<string>().Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
    if (origins.Length > 0) policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
    else if (builder.Environment.IsDevelopment()) policy.WithOrigins("http://localhost:8089", "http://127.0.0.1:8089").AllowAnyHeader().AllowAnyMethod();
}));
builder.Services.AddRateLimiter(options => options.AddPolicy("assistant", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })));
var app = builder.Build();
app.Use(async (context, next) =>
{
    var incoming = context.Request.Headers["X-Correlation-ID"].FirstOrDefault();
    context.TraceIdentifier = Guid.TryParse(incoming, out var parsed) ? parsed.ToString() : Guid.NewGuid().ToString();
    context.Response.Headers["X-Correlation-ID"] = context.TraceIdentifier;
    await next(context);
});
app.UseExceptionHandler(error => error.Run(async context =>
{
    context.Response.StatusCode = 503;
    await Results.Problem(statusCode: 503, title: "Assistant service unavailable.").ExecuteAsync(context);
}));
app.UseStatusCodePages(async (StatusCodeContext context) =>
{
    if (!context.HttpContext.Request.Path.StartsWithSegments("/api/v1")) return;
    await Results.Problem(statusCode: context.HttpContext.Response.StatusCode).ExecuteAsync(context.HttpContext);
});
app.UseCors();
app.UseRateLimiter();
app.MapGet("/health", () => Results.Ok(new { status = "running" }));
app.MapGet("/ready", (IConfiguration configuration) =>
{
    var ready =
        Uri.TryCreate(configuration["Services:IdentityUrl"], UriKind.Absolute, out _) &&
        Uri.TryCreate(configuration["Services:HomeUrl"], UriKind.Absolute, out _) &&
        Uri.TryCreate(configuration["Services:SecurityUrl"], UriKind.Absolute, out _) &&
        Uri.TryCreate(configuration["OpenAI:Endpoint"], UriKind.Absolute, out _) &&
        !string.IsNullOrWhiteSpace(configuration["OpenAI:Deployment"]);
    return ready
        ? Results.Ok(new { status = "ready" })
        : Results.Problem(statusCode: 503, title: "Assistant configuration incomplete.");
});
app.MapGet("/api/v1/assistant/houses/{houseId:guid}/diagnostics/model", async (Guid houseId, HttpContext context, AssistantOperations operations, CancellationToken ct) =>
{
    if (!TryBearer(context, out var auth)) return Results.Problem(statusCode: 401, title: "Authentication required.");
    try { return Results.Ok(await operations.CheckModelDiagnosticsAsync(houseId, auth, ct)); }
    catch (AssistantException ex) { return Problem(ex); }
});
app.MapPost("/api/v1/assistant/houses/{houseId:guid}/ask", async (Guid houseId, AskRequest input, HttpContext context, AssistantOperations operations, CancellationToken ct) =>
{
    if (!TryBearer(context, out var auth)) return Results.Problem(statusCode: 401, title: "Authentication required.");
    try { return Results.Ok(await operations.AskAsync(houseId, input, auth, ct)); }
    catch (AssistantException ex) { return Problem(ex); }
}).RequireRateLimiting("assistant");
app.Run();

static bool TryBearer(HttpContext context, out AuthenticationHeaderValue auth)
{
    auth = null!;
    return AuthenticationHeaderValue.TryParse(context.Request.Headers.Authorization, out var parsed) &&
           string.Equals(parsed.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) &&
           !string.IsNullOrWhiteSpace(parsed.Parameter) &&
           (auth = parsed) is not null;
}

static IResult Problem(AssistantException ex) =>
    Results.Problem(statusCode: ex.StatusCode, title: ex.Title);

public partial class Program { }
