using System.Security.Claims;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using CasaDomotica.Security.Application;
using CasaDomotica.Security.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var operatorFlags = new[] { "--migrate", "--provision-runtime" };
if (args.Any(operatorFlags.Contains))
{
    var builder = Host.CreateApplicationBuilder(args.Where(x => !operatorFlags.Contains(x)).ToArray());
    ConfigureDatabase(builder.Services, builder.Configuration);
    builder.Services.AddScoped<SecurityAdministration>();
    using var host = builder.Build(); using var scope = host.Services.CreateScope();
    var admin = scope.ServiceProvider.GetRequiredService<SecurityAdministration>();
    if (args.Contains("--migrate")) await admin.MigrateAsync(CancellationToken.None);
    if (args.Contains("--provision-runtime")) await admin.ProvisionRuntimeAsync(builder.Configuration["Runtime:Password"] ?? "", CancellationToken.None);
    Console.WriteLine("Security database operation completed."); return;
}
var appBuilder = WebApplication.CreateBuilder(args);
ConfigureDatabase(appBuilder.Services, appBuilder.Configuration);
appBuilder.Services.AddSingleton(TimeProvider.System);
appBuilder.Services.AddScoped<ISecurityEventStore, SqlSecurityEventStore>();
appBuilder.Services.AddScoped<SecurityEvents>();
appBuilder.Services.AddScoped<ICaptureContent, PrivateCaptureContent>();
appBuilder.Services.AddScoped<CaptureCleanup>();
appBuilder.Services.AddScoped<CaptureAnalysis>();
appBuilder.Services.AddHttpClient<IImageAnalyzer, AzureVisionAnalyzer>(client => client.Timeout = TimeSpan.FromSeconds(90)).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
appBuilder.Services.AddSingleton<SecurityServiceTokenCache>();
appBuilder.Services.AddHttpClient<ISecurityAuthorizer, SecurityAuthorizer>(client => client.Timeout = TimeSpan.FromSeconds(45));
appBuilder.Services.AddHttpClient("home", client => client.Timeout = TimeSpan.FromSeconds(45));
appBuilder.Services.AddHostedService<SecurityBusWorker>();
appBuilder.Services.AddHostedService<CaptureCleanupWorker>();
appBuilder.Services.AddHostedService<CaptureAnalysisWorker>();
appBuilder.Services.AddSingleton<IdentityKey>();
appBuilder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(_ => { });
appBuilder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure<IdentityKey>((options, key) =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new() { ValidIssuer = "Identity", ValidAudience = "Security.Public", IssuerSigningKey = key.SecurityKey, ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256], ValidateIssuer = true, ValidateAudience = true, ValidateIssuerSigningKey = true, ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true, ClockSkew = TimeSpan.FromSeconds(10) };
});
appBuilder.Services.AddAuthorization();
appBuilder.Services.AddCors(options => options.AddDefaultPolicy(policy => { var origins = appBuilder.Configuration.GetSection("Cors:Origins").GetChildren().Select(x => x.Value).OfType<string>().ToArray(); if (origins.Length > 0) policy.WithOrigins(origins).AllowAnyMethod().AllowAnyHeader(); }));
var app = appBuilder.Build();
_ = app.Services.GetRequiredService<IdentityKey>();
app.UseExceptionHandler(error => error.Run(async context =>
{
    var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    var status = exception is SecurityAuthorizationException { Code: "sessionRevoked" } ? 401 : 503;
    await Results.Problem(statusCode: status, title: status == 401 ? "Session revoked." : "Security service unavailable.").ExecuteAsync(context);
}));
app.UseCors(); app.UseAuthentication(); app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "running" }));
app.MapGet("/ready", async (SecurityDb db, CancellationToken ct) => { try { await db.Database.OpenConnectionAsync(ct); await db.Database.CloseConnectionAsync(); return Results.Ok(new { status = "ready" }); } catch { return Results.Problem(statusCode: 503, title: "Security storage unavailable."); } });
var routes = app.MapGroup("/api/v1/security/houses/{houseId:guid}").RequireAuthorization();
routes.MapGet("/diagnostics/bus", async (Guid houseId, ClaimsPrincipal user, ISecurityAuthorizer auth, IConfiguration config, CancellationToken ct) =>
{
    if (!await CanRead(user, houseId, auth, ct)) return Results.Forbid();
    var host = config["ServiceBus:Namespace"];
    if (string.IsNullOrWhiteSpace(host)) return Results.Problem(statusCode: 503, title: "Security event transport unavailable.");
    try
    {
        await using var client = new ServiceBusClient(host, new DefaultAzureCredential(), new ServiceBusClientOptions { TransportType = ServiceBusTransportType.AmqpWebSockets });
        await using var receiver = client.CreateReceiver("house-events-v1", "security-v1");
        // Peeking verifies identity and connectivity without locking or consuming a real event.
        _ = await receiver.PeekMessageAsync(cancellationToken: ct);
        return Results.Ok(new { status = "ready" });
    }
    catch (Exception error) when (error is not OperationCanceledException) { return Results.Problem(statusCode: 503, title: "Security event transport unavailable."); }
});
routes.MapGet("/diagnostics/capture-storage", async (Guid houseId, ClaimsPrincipal user, ISecurityAuthorizer auth, IConfiguration config, CancellationToken ct) =>
{
    if (!await CanManage(user, houseId, auth, ct)) return Results.Forbid();
    var root = config["Storage:AccountUrl"];
    if (!Uri.TryCreate(root, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) return Results.Problem(statusCode: 503, title: "Capture storage unavailable.");
    try
    {
        var container = new Azure.Storage.Blobs.BlobServiceClient(uri, new DefaultAzureCredential()).GetBlobContainerClient("captures");
        _ = await container.GetPropertiesAsync(cancellationToken: ct);
        return Results.Ok(new { status = "ready" });
    }
    catch (Exception error) when (error is not OperationCanceledException) { return Results.Problem(statusCode: 503, title: "Capture storage unavailable."); }
});
routes.MapGet("/mode", async (Guid houseId, ClaimsPrincipal user, ISecurityAuthorizer auth, SecurityDb db, HttpContext context, IHttpClientFactory clients, IConfiguration config, CancellationToken ct) =>
{
    if (!await CanRead(user, houseId, auth, ct)) return Results.Forbid();
    // Home is the authoritative source for the current resource association. Reading
    // through it avoids serving a stale projection after a controller is removed.
    var root = config["Home:BaseUrl"]?.TrimEnd('/');
    if (!Uri.TryCreate(root, UriKind.Absolute, out var uri) || uri.Scheme != "https" && !uri.IsLoopback || !AuthenticationHeaderValue.TryParse(context.Request.Headers.Authorization, out var bearer) || !string.Equals(bearer.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)) return Results.Problem(statusCode: 503, title: "House service unavailable.");
    using var request = new HttpRequestMessage(HttpMethod.Get, root + $"/api/v1/houses/{houseId}/security"); request.Headers.Authorization = bearer;
    using var response = await clients.CreateClient("home").SendAsync(request, ct);
    if (response.StatusCode == HttpStatusCode.NotFound) return Results.NotFound();
    if (response.StatusCode == HttpStatusCode.Unauthorized) return Results.Unauthorized();
    if (response.StatusCode == HttpStatusCode.Forbidden) return Results.Forbid();
    if (response.StatusCode == HttpStatusCode.Conflict) return Results.Conflict();
    if (!response.IsSuccessStatusCode) return Results.Problem(statusCode: 503, title: "House service unavailable.");
    using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
    var state = body.RootElement;
    var mode = state.GetProperty("mode").GetString();
    var updated = state.GetProperty("updatedAtUtc").GetDateTimeOffset();
    var received = state.GetProperty("receivedAtUtc").GetDateTimeOffset();
    if (mode is not ("armed" or "disarmed") || updated.Offset != TimeSpan.Zero || received.Offset != TimeSpan.Zero) return Results.Problem(statusCode: 503, title: "House state is invalid.");
    var incident = await db.Incidents.AsNoTracking().Where(x => x.HouseId == houseId).OrderByDescending(x => x.OccurredAtUtc).ThenByDescending(x => x.EventId).FirstOrDefaultAsync(ct);
    return Results.Ok(new { mode, updatedAtUtc = updated, receivedAtUtc = received, incident = incident is null ? null : new { id = incident.EventId, title = "Movimiento con seguridad armada", description = incident.RoomId, occurredAtUtc = incident.OccurredAtUtc, receivedAtUtc = incident.ReceivedAtUtc } });
});
routes.MapGet("/activity", async (Guid houseId, string? type, string? result, string? date, string? cursor, ClaimsPrincipal user, ISecurityAuthorizer auth, SecurityDb db, CancellationToken ct) =>
{
    if (!await CanRead(user, houseId, auth, ct)) return Results.Forbid();
    if (type?.Length > 64 || result?.Length > 32 || !TryCursor(cursor, out var after)) return Results.BadRequest();
    var query = db.Activity.AsNoTracking().Where(x => x.HouseId == houseId);
    if (!string.IsNullOrWhiteSpace(type)) query = query.Where(x => x.Type == type);
    if (!string.IsNullOrWhiteSpace(result)) query = query.Where(x => x.Result == result);
    if (!string.IsNullOrWhiteSpace(date))
    {
        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var day)) return Results.BadRequest();
        var start = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)); var end = start.AddDays(1);
        query = query.Where(x => x.OccurredAtUtc >= start && x.OccurredAtUtc < end);
    }
    if (after is not null) query = query.Where(x => x.OccurredAtUtc < after.Time || x.OccurredAtUtc == after.Time && x.EventId.CompareTo(after.Id) < 0);
    var rows = await query.OrderByDescending(x => x.OccurredAtUtc).ThenByDescending(x => x.EventId).Take(51).ToListAsync(ct);
    var next = rows.Count > 50 ? EncodeCursor(rows[49].OccurredAtUtc, rows[49].EventId) : null;
    return Results.Ok(new { items = rows.Take(50).Select(x => new { id = x.EventId, title = x.Type == "motion" ? "Movimiento detectado" : "Operación de acceso", description = x.RoomId, type = x.Type, result = x.Result, occurredAtUtc = x.OccurredAtUtc, receivedAtUtc = x.ReceivedAtUtc }), nextCursor = next });
});
routes.MapGet("/activity/{eventId:guid}", async (Guid houseId, Guid eventId, ClaimsPrincipal user, ISecurityAuthorizer auth, SecurityDb db, CancellationToken ct) =>
{
    if (!await CanRead(user, houseId, auth, ct)) return Results.Forbid();
    var row = await db.Activity.AsNoTracking().SingleOrDefaultAsync(x => x.HouseId == houseId && x.EventId == eventId, ct);
    return row is null ? Results.NotFound() : Results.Ok(new { id = row.EventId, title = row.Type == "motion" ? "Movimiento detectado" : "Operación de acceso", description = row.RoomId, type = row.Type, result = row.Result, occurredAtUtc = row.OccurredAtUtc, receivedAtUtc = row.ReceivedAtUtc });
});
routes.MapGet("/incidents", async (Guid houseId, string? date, string? cursor, ClaimsPrincipal user, ISecurityAuthorizer auth, SecurityDb db, CancellationToken ct) =>
{
    if (!await CanRead(user, houseId, auth, ct)) return Results.Forbid();
    if (!TryCursor(cursor, out var after)) return Results.BadRequest();
    var query = db.Incidents.AsNoTracking().Where(x => x.HouseId == houseId);
    if (!string.IsNullOrWhiteSpace(date))
    {
        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var day)) return Results.BadRequest();
        var start = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)); var end = start.AddDays(1);
        query = query.Where(x => x.OccurredAtUtc >= start && x.OccurredAtUtc < end);
    }
    if (after is not null) query = query.Where(x => x.OccurredAtUtc < after.Time || x.OccurredAtUtc == after.Time && x.EventId.CompareTo(after.Id) < 0);
    var rows = await query.OrderByDescending(x => x.OccurredAtUtc).ThenByDescending(x => x.EventId).Take(51).ToListAsync(ct);
    var next = rows.Count > 50 ? EncodeCursor(rows[49].OccurredAtUtc, rows[49].EventId) : null;
    return Results.Ok(new { items = rows.Take(50).Select(x => new { id = x.EventId, title = "Movimiento con seguridad armada", description = x.RoomId, type = "incident", occurredAtUtc = x.OccurredAtUtc, receivedAtUtc = x.ReceivedAtUtc }), nextCursor = next });
});
routes.MapGet("/incidents/{eventId:guid}", async (Guid houseId, Guid eventId, ClaimsPrincipal user, ISecurityAuthorizer auth, SecurityDb db, CancellationToken ct) =>
{
    if (!await CanRead(user, houseId, auth, ct)) return Results.Forbid();
    var row = await db.Incidents.AsNoTracking().SingleOrDefaultAsync(x => x.HouseId == houseId && x.EventId == eventId, ct);
    return row is null ? Results.NotFound() : Results.Ok(new { id = row.EventId, title = "Movimiento con seguridad armada", description = row.RoomId, type = "incident", occurredAtUtc = row.OccurredAtUtc, receivedAtUtc = row.ReceivedAtUtc });
});
routes.MapGet("/captures", async (Guid houseId, string? cursor, ClaimsPrincipal user, ISecurityAuthorizer auth, SecurityDb db, CancellationToken ct) =>
{
    if (!await CanRead(user, houseId, auth, ct)) return Results.Forbid();
    if (!TryCursor(cursor, out var after)) return Results.BadRequest();
    var query = db.Captures.AsNoTracking().Where(x => x.HouseId == houseId && x.DeleteRequestedAtUtc == null);
    if (after is not null) query = query.Where(x => x.OccurredAtUtc < after.Time || x.OccurredAtUtc == after.Time && x.CaptureId.CompareTo(after.Id) < 0);
    var rows = await query.OrderByDescending(x => x.OccurredAtUtc).ThenByDescending(x => x.CaptureId).Take(51).ToListAsync(ct);
    var next = rows.Count > 50 ? EncodeCursor(rows[49].OccurredAtUtc, rows[49].CaptureId) : null;
    return Results.Ok(new { items = rows.Take(50).Select(x => new { id = x.CaptureId, title = "Captura de la casa", status = x.AnalysisStatus, occurredAtUtc = x.OccurredAtUtc, receivedAtUtc = x.ReceivedAtUtc }), nextCursor = next });
});
routes.MapGet("/captures/{captureId:guid}", async (Guid houseId, Guid captureId, ClaimsPrincipal user, ISecurityAuthorizer auth, SecurityDb db, CancellationToken ct) =>
{
    if (!await CanRead(user, houseId, auth, ct)) return Results.Forbid();
    var row = await db.Captures.AsNoTracking().SingleOrDefaultAsync(x => x.HouseId == houseId && x.CaptureId == captureId && x.DeleteRequestedAtUtc == null, ct);
    return row is null ? Results.NotFound() : Results.Ok(new { id = row.CaptureId, title = "Captura de la casa", status = row.AnalysisStatus, analysis = row.AnalysisText, occurredAtUtc = row.OccurredAtUtc, receivedAtUtc = row.ReceivedAtUtc });
});
routes.MapGet("/captures/{captureId:guid}/content", async (Guid houseId, Guid captureId, ClaimsPrincipal user, ISecurityAuthorizer auth, SecurityDb db, ICaptureContent content, HttpContext context, CancellationToken ct) =>
{
    if (!await CanRead(user, houseId, auth, ct)) return Results.Forbid();
    var row = await db.Captures.AsNoTracking().SingleOrDefaultAsync(x => x.HouseId == houseId && x.CaptureId == captureId && x.DeleteRequestedAtUtc == null, ct);
    if (row is null) return Results.NotFound();
    var image = await content.ReadAsync(row, ct);
    if (image is null) return Results.NotFound();
    context.Response.Headers.CacheControl = "private, no-store";
    return Results.File(image.Bytes, image.ContentType, enableRangeProcessing: false);
});
routes.MapPost("/captures/{captureId:guid}/analysis", async (Guid houseId, Guid captureId, ClaimsPrincipal user, ISecurityAuthorizer auth, SecurityDb db, TimeProvider clock, CancellationToken ct) =>
{
    if (!await CanManage(user, houseId, auth, ct)) return Results.Forbid();
    var row = await db.Captures.AsNoTracking().SingleOrDefaultAsync(x => x.HouseId == houseId && x.CaptureId == captureId && x.DeleteRequestedAtUtc == null, ct);
    if (row is null) return Results.NotFound();
    if (row.AnalysisStatus == "completed") return Results.Ok(new { id = captureId, status = "completed" });
    if (row.AnalysisStatus is "pending" or "analyzing") return Results.Accepted(value: new { id = captureId, status = row.AnalysisStatus });
    await db.Captures.Where(x => x.HouseId == houseId && x.CaptureId == captureId && x.DeleteRequestedAtUtc == null && (x.AnalysisStatus == null || x.AnalysisStatus == "failed")).ExecuteUpdateAsync(update => update.SetProperty(x => x.AnalysisStatus, "pending").SetProperty(x => x.AnalysisText, (string?)null).SetProperty(x => x.AnalysisError, (string?)null).SetProperty(x => x.AnalysisRequestedAtUtc, clock.GetUtcNow()), ct);
    var current = await db.Captures.AsNoTracking().SingleOrDefaultAsync(x => x.HouseId == houseId && x.CaptureId == captureId && x.DeleteRequestedAtUtc == null, ct);
    return current is null ? Results.NotFound() : Results.Accepted(value: new { id = captureId, status = current.AnalysisStatus });
});
routes.MapDelete("/captures/{captureId:guid}", async (Guid houseId, Guid captureId, ClaimsPrincipal user, ISecurityAuthorizer auth, SecurityDb db, TimeProvider clock, CancellationToken ct) =>
{
    if (!await CanManage(user, houseId, auth, ct)) return Results.Forbid();
    var row = await db.Captures.AsNoTracking().SingleOrDefaultAsync(x => x.HouseId == houseId && x.CaptureId == captureId, ct);
    if (row is null) return Results.NotFound();
    if (row.DeletedAtUtc is not null) return Results.Ok(new { id = captureId, status = "deleted" });
    if (row.DeleteRequestedAtUtc is null) await db.Captures.Where(x => x.HouseId == houseId && x.CaptureId == captureId && x.DeleteRequestedAtUtc == null).ExecuteUpdateAsync(update => update.SetProperty(x => x.DeleteRequestedAtUtc, clock.GetUtcNow()), ct);
    return Results.Accepted(value: new { id = captureId, status = row.DeleteError is null ? "pending" : "blocked" });
});
routes.MapGet("/captures/{captureId:guid}/deletion", async (Guid houseId, Guid captureId, ClaimsPrincipal user, ISecurityAuthorizer auth, SecurityDb db, CancellationToken ct) =>
{
    if (!await CanManage(user, houseId, auth, ct)) return Results.Forbid();
    var row = await db.Captures.AsNoTracking().SingleOrDefaultAsync(x => x.HouseId == houseId && x.CaptureId == captureId, ct);
    if (row is null) return Results.NotFound();
    var status = row.DeletedAtUtc is not null ? "deleted" : row.DeleteRequestedAtUtc is null ? "none" : row.DeleteError is null ? "pending" : "blocked";
    return Results.Ok(new { id = captureId, status });
});
routes.MapGet("/capture-retention", async (Guid houseId, ClaimsPrincipal user, ISecurityAuthorizer auth, SecurityDb db, CancellationToken ct) =>
{
    if (!await CanManage(user, houseId, auth, ct)) return Results.Forbid();
    var policy = await db.CapturePolicies.AsNoTracking().SingleOrDefaultAsync(x => x.HouseId == houseId, ct);
    return Results.Ok(new { retentionDays = policy?.RetentionDays });
});
routes.MapPut("/capture-retention", async (Guid houseId, RetentionRequest request, ClaimsPrincipal user, ISecurityAuthorizer auth, SecurityDb db, TimeProvider clock, CancellationToken ct) =>
{
    if (!await CanManage(user, houseId, auth, ct)) return Results.Forbid();
    if (request.RetentionDays is < 1 or > 3650) return Results.BadRequest();
    var policy = await db.CapturePolicies.SingleOrDefaultAsync(x => x.HouseId == houseId, ct);
    if (policy is null) { policy = new CapturePolicyRow { HouseId = houseId }; db.CapturePolicies.Add(policy); }
    policy.RetentionDays = request.RetentionDays; policy.UpdatedAtUtc = clock.GetUtcNow();
    await db.SaveChangesAsync(ct);
    return Results.Ok(new { retentionDays = policy.RetentionDays });
});
app.Run();

static void ConfigureDatabase(IServiceCollection services, IConfiguration configuration) => services.AddDbContext<SecurityDb>(options => options.UseSqlServer(configuration.GetConnectionString("SecurityDb") ?? throw new InvalidOperationException("Security database is required."), sql => sql.CommandTimeout(90)));
static async Task<bool> CanRead(ClaimsPrincipal principal, Guid house, ISecurityAuthorizer auth, CancellationToken ct) => Guid.TryParse(principal.FindFirstValue("sub"), out var user) && Guid.TryParse(principal.FindFirstValue("sid"), out var session) && await auth.CheckAsync(user, session, house, "security:read", Guid.NewGuid(), ct);
static async Task<bool> CanManage(ClaimsPrincipal principal, Guid house, ISecurityAuthorizer auth, CancellationToken ct) => Guid.TryParse(principal.FindFirstValue("sub"), out var user) && Guid.TryParse(principal.FindFirstValue("sid"), out var session) && await auth.CheckAsync(user, session, house, "security:manage", Guid.NewGuid(), ct);
static string EncodeCursor(DateTimeOffset time, Guid id) => Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Cursor(time, id))));
static bool TryCursor(string? raw, out Cursor? cursor)
{
    cursor = null; if (string.IsNullOrEmpty(raw)) return true;
    if (raw.Length > 512) return false;
    try { cursor = JsonSerializer.Deserialize<Cursor>(Convert.FromBase64String(raw)); return cursor is { Id: var id, Time.Offset: var offset } && id != Guid.Empty && offset == TimeSpan.Zero; }
    catch (Exception error) when (error is FormatException or JsonException) { return false; }
}
public sealed record Cursor(DateTimeOffset Time, Guid Id);
public sealed record RetentionRequest([property: System.Text.Json.Serialization.JsonRequired] int? RetentionDays);
public partial class Program { }
