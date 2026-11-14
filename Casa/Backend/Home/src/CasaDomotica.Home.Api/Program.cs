using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using CasaDomotica.Home.Application.Commands;
using CasaDomotica.Home.Domain.Interfaces;
using CasaDomotica.Home.Infrastructure;
using CasaDomotica.Home.Infrastructure.Runtime;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var operatorFlags = new[] { "--migrate", "--provision-runtime", "--bind-device", "--bind-resource" };
if (args.Any(operatorFlags.Contains))
{
    var hostBuilder = Host.CreateApplicationBuilder(args.Where(argument => !operatorFlags.Contains(argument)).ToArray());
    hostBuilder.Services.AddHomeInfrastructure(hostBuilder.Configuration);
    using var host = hostBuilder.Build(); using var scope = host.Services.CreateScope();
    var administration = scope.ServiceProvider.GetRequiredService<HomeAdministration>();
    if (args.Contains("--migrate")) await administration.MigrateAsync(CancellationToken.None);
    if (args.Contains("--provision-runtime")) await administration.ProvisionRuntimeAsync(hostBuilder.Configuration["Runtime:Password"] ?? "", CancellationToken.None);
    if (args.Contains("--bind-device")) await administration.BindDeviceAsync(Guid.Parse(hostBuilder.Configuration["Device:HouseId"] ?? ""), hostBuilder.Configuration["Device:Id"] ?? "", hostBuilder.Configuration["Device:LightId"] ?? "", CancellationToken.None);
    if (args.Contains("--bind-resource")) await administration.BindResourceAsync(Guid.Parse(hostBuilder.Configuration["Device:HouseId"] ?? ""), hostBuilder.Configuration["Device:Id"] ?? "", hostBuilder.Configuration["Device:ResourceId"] ?? "", hostBuilder.Configuration["Device:ResourceKind"] ?? "", hostBuilder.Configuration["Device:ResourceDeviceId"] ?? "", CancellationToken.None);
    Console.WriteLine("Home database operation completed."); return;
}
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHomeInfrastructure(builder.Configuration);
builder.Services.AddScoped<HomeOperations>();
builder.Services.AddScoped<CapabilityOperations>();
builder.Services.AddHostedService<CommandWorker>(); builder.Services.AddHostedService<OutboxWorker>();
builder.Services.AddHostedService<VideoCleanupWorker>();
builder.Services.AddSingleton<HouseSubscriptions>(); builder.Services.AddScoped<IStateNotifier, SignalRStateNotifier>();
builder.Services.AddSignalR().AddAzureSignalR(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("SignalR") ?? throw new InvalidOperationException("Configure the protected SignalR connection.");
    options.AccessTokenLifetime = TimeSpan.FromMinutes(5);
    options.ClaimsProvider = context => context.User.Claims.Where(claim => claim.Type is "sub" or "sid").Concat(new[] { new Claim("access_expires", context.User.FindFirstValue("exp") ?? "0"), new Claim("house_id", context.Request.Query["houseId"].ToString()) });
});
builder.Services.AddProblemDetails(ContractProblems.Configure); builder.Services.AddExceptionHandler<HomeApiErrors>(); builder.Services.AddHealthChecks();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    var origins = builder.Configuration.GetSection("Cors:Origins").GetChildren().Select(x => x.Value).OfType<string>().ToArray();
    if (origins.Length > 0) policy.WithOrigins(origins).AllowAnyMethod().AllowAnyHeader();
}));
builder.Services.AddSingleton<IdentityVerificationKey>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(_ => { });
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure<IdentityVerificationKey>((options, publicKey) =>
{
    options.MapInboundClaims = false;
    options.Events = new JwtBearerEvents { OnMessageReceived = context =>
    {
        if (context.HttpContext.Request.Path.StartsWithSegments("/hubs/home") && context.Request.Query.TryGetValue("access_token", out var token)) context.Token = token;
        return Task.CompletedTask;
    } };
    options.TokenValidationParameters = new() { ValidIssuer = "Identity", ValidAudience = "Home.Public", IssuerSigningKey = publicKey.SecurityKey, ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256], ValidateIssuer = true, ValidateAudience = true, ValidateIssuerSigningKey = true, ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true, ClockSkew = TimeSpan.FromSeconds(10) };
});
// Separate signed service assertions for trusted IoT ingestion; public user tokens cannot invoke it.
builder.Services.AddAuthentication().AddJwtBearer("ingestion", options =>
{
    options.MapInboundClaims = false;
    var pem = builder.Configuration["Ingestion:PublicKeyPem"];
    SecurityKey[] verificationKeys = [];
    if (!string.IsNullOrWhiteSpace(pem)) { var ec = ECDsa.Create(); ec.ImportFromPem(pem); verificationKeys = [new ECDsaSecurityKey(ec)]; }
    options.TokenValidationParameters = new() { ValidIssuer = "service:home-functions", ValidAudience = "Home.Internal", IssuerSigningKeys = verificationKeys, ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256], ValidateIssuer = true, ValidateAudience = true, ValidateIssuerSigningKey = true, ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true, ClockSkew = TimeSpan.FromSeconds(10) };
});
builder.Services.AddAuthorization(options => options.AddPolicy("ingestion", policy => policy.AddAuthenticationSchemes("ingestion").RequireAuthenticatedUser().RequireClaim("sub", "service:home-functions").RequireClaim("scope", "home:ingest")));
var app = builder.Build();
_ = app.Services.GetRequiredService<IdentityVerificationKey>(); // Fail closed at startup if the trusted key is missing or invalid.
app.Use(ContractProblems.Correlate);
app.UseExceptionHandler();
app.UseStatusCodePages(ContractProblems.WriteStatus); app.UseCors(); app.UseAuthentication(); app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapVideoEndpoints();
app.MapPost("/internal/captures/{captureId:guid}/upload", async (Guid captureId, HttpContext context, CaptureUploadService upload, CancellationToken ct) =>
{
    if (!context.Request.Headers.TryGetValue("X-Capture-Token", out var values) || values.Count != 1) throw new HomeException("unauthorized", "Capture upload is not authorized.");
    var result = await upload.UploadAsync(captureId, values[0] ?? "", context.Request.ContentType ?? "", context.Request.ContentLength, context.Request.Body, ct);
    return Results.Ok(new { captureId = result.CaptureId, blobName = result.BlobName });
});
app.MapHub<HouseHub>("/hubs/home", options => options.CloseOnAuthenticationExpiration = true).RequireAuthorization();
app.MapGet("/ready", async (HomeDb db, CancellationToken ct) =>
{
    try { await db.Database.OpenConnectionAsync(ct); await db.Database.CloseConnectionAsync(); return Results.Ok(new { status = "ready" }); }
    catch (SqlException error) { var address = error.Number == 40615 ? System.Text.RegularExpressions.Regex.Match(error.Message, @"Client with IP address '([0-9.]+)'").Groups[1].Value : ""; return Results.Problem(statusCode: 503, title: "Home storage unavailable", extensions: new Dictionary<string, object?> { ["dependencyErrorCode"] = error.Number, ["dependencyClientAddress"] = address }); }
});
app.MapGet("/api/v1/houses/{houseId:guid}/status", async (Guid houseId, ClaimsPrincipal user, HomeOperations operations, RuntimeWakeups wakeups, CancellationToken ct) =>
{
    var status = await operations.StatusAsync(Context(user), houseId, ct); wakeups.Commands();
    return status is null ? Results.Problem(statusCode: 404, title: "No device report is available.", extensions: new Dictionary<string, object?> { ["code"] = "noReport" }) : Results.Ok(new { houseId = status.HouseId, updatedAtUtc = status.UpdatedAtUtc, lights = status.Lights.Select(light => new { lightId = light.LightId, lightOn = light.LightOn, lightingMode = light.LightingMode, stateRevision = light.StateRevision, stateReportedAtUtc = light.StateReportedAtUtc, lastCommand = Result(light.LastCommand), pendingCommand = light.PendingCommand is null ? null : Resource(light.PendingCommand) }) });
}).RequireAuthorization();
app.MapPost("/api/v1/houses/{houseId:guid}/lights/{lightId}/commands", async (Guid houseId, string lightId, LightCommandRequest request, ClaimsPrincipal user, HomeOperations operations, RuntimeWakeups wakeups, CancellationToken ct) =>
{
    var command = await operations.SendAsync(Context(user), houseId, lightId, request.Type, request.Payload, request.IdempotencyKey, request.CorrelationId ?? Guid.NewGuid(), ct);
    if (command.Status == "pending") wakeups.Commands();
    return command.Status == "pending" ? Results.Accepted($"/api/v1/commands/{Uri.EscapeDataString(command.CommandId)}", new { commandId = command.CommandId, status = command.Status, correlationId = command.CorrelationId }) : Results.Ok(Resource(command));
}).RequireAuthorization();
app.MapPost("/api/v1/houses/{houseId:guid}/resources/{resourceId}/commands", async (Guid houseId, string resourceId, CapabilityCommandRequest request, ClaimsPrincipal user, CapabilityOperations operations, RuntimeWakeups wakeups, CancellationToken ct) =>
{
    var command = await operations.SendAsync(Context(user), houseId, resourceId, request.Type, request.Payload, request.IdempotencyKey, request.CorrelationId ?? Guid.NewGuid(), request.ExpiresAtUtc, ct);
    if (command.Status == "pending") wakeups.Commands();
    return command.Status == "pending" ? Results.Accepted($"/api/v1/commands/{Uri.EscapeDataString(command.CommandId)}", new { commandId = command.CommandId, status = command.Status, correlationId = command.CorrelationId }) : Results.Ok(Resource(command));
}).RequireAuthorization();
app.MapGet("/api/v1/commands/{commandId}", async (string commandId, ClaimsPrincipal user, HomeOperations operations, CancellationToken ct) =>
{
    var command = await operations.CommandAsync(Context(user), commandId, ct); return command is null ? Results.NotFound() : Results.Ok(Resource(command));
}).RequireAuthorization();
app.MapGet("/api/v1/houses/{houseId:guid}/commands/by-key/{key}", async (Guid houseId, string key, ClaimsPrincipal user, HomeOperations operations, CancellationToken ct) =>
{
    var command = await operations.ByKeyAsync(Context(user), houseId, key, ct); return command is null ? Results.NotFound() : Results.Ok(Resource(command));
}).RequireAuthorization();
app.MapGet("/api/v1/houses/{houseId:guid}/resources", async (Guid houseId, ClaimsPrincipal user, IUserAuthorizer authorizer, HomeDb db, CancellationToken ct) =>
{
    if (!await authorizer.CheckAsync(Context(user), houseId, "home:read", Guid.NewGuid(), ct)) throw new HomeException("forbidden", "The user is not authorized for this house.");
    var resources = await (from resource in db.Resources.AsNoTracking() join device in db.Devices.AsNoTracking() on resource.DeviceId equals device.DeviceId where resource.HouseId == houseId && resource.Enabled && device.Enabled && device.HouseId == houseId select new { resource.ResourceId, resource.Kind, device.LastReportedAtUtc }).ToListAsync(ct);
    var pending = await db.Commands.AsNoTracking().Where(x => x.HouseId == houseId && x.Active).ToListAsync(ct);
    return Results.Ok(resources.Select(resource => new { resource.ResourceId, resource.Kind, deviceReportedAtUtc = resource.LastReportedAtUtc, pendingCommand = pending.Where(x => x.LightId == resource.ResourceId).Select(x => Resource(SqlHomeRuntime.Map(x))).FirstOrDefault() }));
}).RequireAuthorization();
app.MapGet("/api/v1/houses/{houseId:guid}/telemetry", async (Guid houseId, ClaimsPrincipal user, IUserAuthorizer authorizer, HomeDb db, CancellationToken ct) =>
{
    if (!await authorizer.CheckAsync(Context(user), houseId, "home:read", Guid.NewGuid(), ct)) throw new HomeException("forbidden", "The user is not authorized for this house.");
    var rows = await db.Telemetry.AsNoTracking().Where(x => x.HouseId == houseId && x.Type == "TelemetryReported")
        .OrderByDescending(x => x.OccurredAtUtc).ThenByDescending(x => x.ReceivedAtUtc).Take(100).ToListAsync(ct);
    var result = new List<object>();
    foreach (var row in rows.GroupBy(x => x.SensorId).Select(x => x.First()))
    {
        using var payload = JsonDocument.Parse(row.PayloadJson);
        result.Add(new { sensorId = row.SensorId + ":light", name = "Luz ambiental", value = payload.RootElement.GetProperty("lightPercent").GetDouble(), unit = "%", row.OccurredAtUtc, row.ReceivedAtUtc });
        result.Add(new { sensorId = row.SensorId + ":motion", name = "Movimiento", value = payload.RootElement.GetProperty("motion").GetBoolean(), unit = (string?)null, row.OccurredAtUtc, row.ReceivedAtUtc });
    }
    return Results.Ok(result);
}).RequireAuthorization();
app.MapGet("/api/v1/houses/{houseId:guid}/security", async (Guid houseId, ClaimsPrincipal user, IUserAuthorizer authorizer, HomeDb db, CancellationToken ct) =>
{
    if (!await authorizer.CheckAsync(Context(user), houseId, "home:read", Guid.NewGuid(), ct)) throw new HomeException("forbidden", "The user is not authorized for this house.");
    var devices = await (from resource in db.Resources.AsNoTracking() join device in db.Devices.AsNoTracking() on resource.DeviceId equals device.DeviceId where resource.HouseId == houseId && resource.Kind == "security" && resource.Enabled && device.Enabled && device.HouseId == houseId select resource.DeviceId).Take(2).ToListAsync(ct);
    if (devices.Count == 0) return Results.NotFound();
    if (devices.Count != 1) return Results.Problem(statusCode: 409, title: "Security resource configuration is ambiguous.");
    var deviceId = devices[0];
    var report = await db.Telemetry.AsNoTracking().Where(x => x.HouseId == houseId && x.SensorId == deviceId && (x.Type == "TelemetryReported" || x.Type == "DeviceStateReported"))
        .OrderByDescending(x => x.OccurredAtUtc).ThenByDescending(x => x.ReceivedAtUtc).FirstOrDefaultAsync(ct);
    if (report is null) return Results.NotFound();
    using var payload = JsonDocument.Parse(report.PayloadJson);
    var mode = payload.RootElement.GetProperty("securityModeApplied").GetString();
    if (mode is not ("armed" or "disarmed")) throw new HomeException("unavailable", "Invalid security state.");
    return Results.Ok(new { mode, updatedAtUtc = report.OccurredAtUtc, receivedAtUtc = report.ReceivedAtUtc });
}).RequireAuthorization();
app.MapPost("/api/v1/internal/device-events", async (IngestionRequest request, DeviceIngestion ingestion, RuntimeWakeups wakeups, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.SourceDeviceId) || request.SourceDeviceId.Length > 128) return Results.BadRequest();
    await ingestion.IngestAsync(request.SourceDeviceId, request.Envelope, ct); wakeups.Outbox(); return Results.NoContent();
}).RequireAuthorization("ingestion");
app.Run();
static UserContext Context(ClaimsPrincipal principal)
{
    if (!Guid.TryParse(principal.FindFirstValue("sub"), out var userId) || !Guid.TryParse(principal.FindFirstValue("sid"), out var sessionId)) throw new HomeException("unauthorized", "Invalid user session.");
    return new(userId, sessionId);
}
static JsonElement? Result(StoredCommand? command) => command?.ResultJson is null ? null : JsonSerializer.Deserialize<JsonElement>(command.ResultJson);
static object Resource(StoredCommand command) => command.Type is "SetLight" or "SetLightingMode"
    ? new { commandId = command.CommandId, houseId = command.HouseId, lightId = command.LightId, status = command.Status, type = command.Type, requestedAtUtc = command.RequestedAtUtc, correlationId = command.CorrelationId, result = Result(command) }
    : new { commandId = command.CommandId, houseId = command.HouseId, resourceId = command.LightId, status = command.Status, type = command.Type, requestedAtUtc = command.RequestedAtUtc, expiresAtUtc = command.ExpiresAtUtc, correlationId = command.CorrelationId, result = Result(command) };
public sealed record LightCommandRequest(string Type, JsonElement Payload, string IdempotencyKey, Guid? CorrelationId);
public sealed record CapabilityCommandRequest(string Type, JsonElement Payload, string IdempotencyKey, Guid? CorrelationId, DateTimeOffset ExpiresAtUtc);
public sealed record IngestionRequest(string SourceDeviceId, JsonElement Envelope);
public sealed class HomeApiErrors : Microsoft.AspNetCore.Diagnostics.IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception error, CancellationToken ct)
    {
        var code = error is HomeException home ? home.Code : "unavailable";
        if (error is not (HomeException or SqlException or DbUpdateException)) return false;
        var status = code switch { "invalidPayload" or "invalidEvent" => 400, "unauthorized" or "sessionRevoked" => 401, "forbidden" => 403, "notFound" or "unknownDevice" => 404, "conflict" or "duplicateChanged" or "pendingCommand" or "manualModeRequired" => 409, "requestExpired" => 422, _ => 503 };
        context.Response.StatusCode = status;
        await Results.Problem(statusCode: status, title: error is HomeException ? error.Message : "Home storage unavailable", extensions: new Dictionary<string, object?> { ["code"] = code, ["correlationId"] = context.TraceIdentifier }).ExecuteAsync(context); return true;
    }
}
public sealed class CommandWorker(IServiceScopeFactory factory, RuntimeWakeups wakeups, ILogger<CommandWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        wakeups.Commands();
        while (!stoppingToken.IsCancellationRequested)
        {
            await wakeups.WaitCommands(stoppingToken);
            var more = true;
            while (more && !stoppingToken.IsCancellationRequested)
            {
                try { using var scope = factory.CreateScope(); more = await scope.ServiceProvider.GetRequiredService<IDeviceDispatcher>().DispatchNextAsync(stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                catch (Exception error) { logger.LogWarning("Home command dispatcher unavailable: {Type}", error.GetType().Name); await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); }
                if (more) await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }
}
public sealed class OutboxWorker(IServiceScopeFactory factory, RuntimeWakeups wakeups, ILogger<OutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        wakeups.Outbox();
        while (!stoppingToken.IsCancellationRequested)
        {
            await wakeups.WaitOutbox(stoppingToken);
            var more = true;
            while (more && !stoppingToken.IsCancellationRequested)
            {
                try { using var scope = factory.CreateScope(); more = await scope.ServiceProvider.GetRequiredService<OutboxPublisher>().PublishNextAsync(stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                catch (Exception error) { logger.LogWarning("Home event publisher unavailable: {Type}", error.GetType().Name); await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken); }
            }
        }
    }
}
public partial class Program { }
