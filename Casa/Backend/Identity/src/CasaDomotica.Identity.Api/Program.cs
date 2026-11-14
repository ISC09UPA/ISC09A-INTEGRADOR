using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.RateLimiting;
using CasaDomotica.Identity.Application.Authentication;
using CasaDomotica.Identity.Application.Common;
using CasaDomotica.Identity.Domain.Interfaces;
using CasaDomotica.Identity.Infrastructure;
using CasaDomotica.Identity.Infrastructure.Persistence;
using CasaDomotica.Identity.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

if (args.Contains("--migrate") || args.Contains("--bootstrap-admin") || args.Contains("--provision-runtime"))
{
    var operatorBuilder = Host.CreateApplicationBuilder(args.Where(argument => argument is not ("--migrate" or "--bootstrap-admin" or "--provision-runtime")).ToArray());
    operatorBuilder.Services.AddIdentityInfrastructure(operatorBuilder.Configuration);
    using var host = operatorBuilder.Build();
    using var scope = host.Services.CreateScope();
    var administration = scope.ServiceProvider.GetRequiredService<IdentityAdministration>();
    if (args.Contains("--migrate")) { await administration.MigrateAsync(CancellationToken.None); Console.WriteLine("Identity schema migration completed."); }
    if (args.Contains("--provision-runtime")) { await administration.ProvisionRuntimeAsync(operatorBuilder.Configuration["Runtime:Password"] ?? "", CancellationToken.None); Console.WriteLine("Identity runtime database permissions configured."); }
    if (args.Contains("--bootstrap-admin"))
    {
        var configuration = operatorBuilder.Configuration;
        var result = await administration.BootstrapAsync(configuration["Bootstrap:Email"] ?? "", configuration["Bootstrap:Password"] ?? "", configuration["Bootstrap:HouseName"] ?? "Mi casa", CancellationToken.None);
        Console.WriteLine($"Administrator created. UserId={result.UserId}; HouseId={result.HouseId}");
    }
    return;
}

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddIdentityInfrastructure(builder.Configuration);
builder.Services.AddScoped<Login>();
builder.Services.AddScoped<AuthorizeUser>();
builder.Services.AddProblemDetails(ContractProblems.Configure);
builder.Services.AddExceptionHandler<UnavailableHandler>();
builder.Services.AddHealthChecks();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    var origins = builder.Configuration.GetSection("Cors:Origins").GetChildren().Select(x => x.Value).OfType<string>().ToArray();
    if (origins.Length > 0) policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
}));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("authentication", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    // Service token refreshes must not consume the login and logout budget, even
    // when Azure's reverse proxy presents both requests from the same address.
    options.AddPolicy("service-authentication", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, _ => {})
    .AddJwtBearer("service", _ => {});
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure<JwtTokens>((options, signer) =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = Validation(signer.VerificationKey, "Identity.Public");
    options.Events = new JwtBearerEvents { OnTokenValidated = async context =>
    {
        var sub = context.Principal?.FindFirstValue("sub"); var sid = context.Principal?.FindFirstValue("sid");
        if (!Guid.TryParse(sub, out var userId) || !Guid.TryParse(sid, out var sessionId) || !await context.HttpContext.RequestServices.GetRequiredService<ISessionRepository>().IsActiveAsync(sessionId, userId, DateTimeOffset.UtcNow, context.HttpContext.RequestAborted)) context.Fail("Session is not active.");
    } };
});
builder.Services.AddOptions<JwtBearerOptions>("service").Configure<JwtTokens>((options, signer) =>
{
    options.MapInboundClaims = false; options.TokenValidationParameters = Validation(signer.VerificationKey, "Identity.Internal");
});
builder.Services.AddAuthorization(options => options.AddPolicy("trusted-service", policy => policy.AddAuthenticationSchemes("service").RequireAuthenticatedUser().RequireClaim("scope", "identity:authorize").RequireAssertion(context => { var client = context.User.FindFirstValue("client_id"); return ServiceAuthorization.IsKnownClient(client) && context.User.FindFirstValue("sub") == "service:" + client; })));
var app = builder.Build();
_ = app.Services.GetRequiredService<JwtTokens>(); // Startup fails closed without a valid signing key.
app.Use(ContractProblems.Correlate);
app.UseExceptionHandler();
app.UseStatusCodePages(ContractProblems.WriteStatus);
app.UseCors(); app.UseRateLimiter(); app.UseAuthentication(); app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapGet("/ready", async (IdentityDb db, CancellationToken ct) =>
{
    try
    {
        await db.Database.OpenConnectionAsync(ct);
        await db.Database.CloseConnectionAsync();
        return Results.Ok(new { status = "ready" });
    }
    catch (SqlException error)
    {
        // Numeric dependency diagnostics only: no connection strings, SQL text or credentials.
        var address = error.Number == 40615 ? System.Text.RegularExpressions.Regex.Match(error.Message, @"Client with IP address '([0-9.]+)'").Groups[1].Value : "";
        return Results.Problem(statusCode: 503, title: "Identity storage unavailable", extensions: new Dictionary<string, object?> { ["dependencyErrorCode"] = error.Number, ["dependencyClientAddress"] = address });
    }
});
app.MapGet("/api/v1/identity/.well-known/jwks.json", (JwtTokens tokens) => Results.Ok(tokens.PublicJwks()));
app.MapPost("/api/v1/identity/auth/login", async (LoginRequest request, Login login, CancellationToken ct) =>
{
    if (request.Email is null || request.Password is null || request.Email.Length > 254) return InvalidCredentials();
    return SessionResult(await login.ExecuteAsync(request.Email, request.Password, ct));
}).RequireRateLimiting("authentication");
app.MapPost("/api/v1/identity/auth/refresh", async (RefreshRequest request, Login login, CancellationToken ct) => string.IsNullOrEmpty(request.RefreshToken) ? InvalidCredentials() : SessionResult(await login.RefreshAsync(request.RefreshToken, ct))).RequireRateLimiting("authentication");
app.MapPost("/api/v1/identity/auth/logout", async (RefreshRequest request, Login login, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.RefreshToken) || request.RefreshToken.Length > 256) return Results.BadRequest();
    await login.LogoutAsync(request.RefreshToken, ct); return Results.NoContent();
}).RequireRateLimiting("authentication");
app.MapGet("/api/v1/identity/me", async (ClaimsPrincipal principal, IdentityDb db, CancellationToken ct) =>
{
    var id = Guid.Parse(principal.FindFirstValue("sub")!);
    var user = await db.Users.AsNoTracking().SingleAsync(x => x.Id == id, ct);
    var houses = await (from membership in db.Memberships.AsNoTracking() join house in db.Houses.AsNoTracking() on membership.HouseId equals house.Id where membership.UserId == id select new { houseId = house.Id, name = house.Name, role = membership.Role }).ToListAsync(ct);
    return Results.Ok(new { userId = id, email = user.Email, houses });
}).RequireAuthorization();
app.MapPost("/api/v1/identity/internal/token", async (AssertionRequest request, IConfiguration configuration, JwtTokens signer, IdentityDb db, CancellationToken ct) =>
{
    if (request.GrantType != "urn:ietf:params:oauth:grant-type:jwt-bearer" || string.IsNullOrEmpty(request.Assertion) || request.Assertion.Length > 8192) return InvalidCredentials();
    // An unverified client identifier selects an allowlisted public key; signature and all claims are then validated.
    var assertion = new ServiceAssertionValidator(configuration).Validate(request.Assertion);
    if (assertion is null) return InvalidCredentials();
    db.Assertions.Add(new() { Jti = assertion.Jti, ExpiresAt = assertion.ExpiresAt });
    try { await db.SaveChangesAsync(ct); } catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 }) { return InvalidCredentials(); }
    return Results.Ok(new { accessToken = signer.GenerateServiceToken(assertion.ClientId, "identity:authorize"), tokenType = "Bearer", expiresIn = 300 });
}).RequireRateLimiting("service-authentication");
app.MapPost("/api/v1/identity/internal/authz/check", async (AuthorizationRequest request, ClaimsPrincipal principal, AuthorizeUser authorization, CancellationToken ct) =>
{
    if (!ServiceAuthorization.CanCheck(principal.FindFirstValue("client_id"), request.Action)) return Results.Problem(statusCode: 403, title: "Service not authorized for this action.");
    var decision = await authorization.EvaluateAsync(request.UserId, request.SessionId, request.HouseId, request.Action, ct);
    if (decision != AuthorizationOutcome.Allowed) return Results.Problem(statusCode: 403, title: "Permission denied", extensions: new Dictionary<string, object?> { ["code"] = decision == AuthorizationOutcome.SessionRevoked ? "sessionRevoked" : "forbidden", ["correlationId"] = request.CorrelationId });
    return Results.Ok(new { decision = "allowed", checkedAtUtc = DateTimeOffset.UtcNow, correlationId = request.CorrelationId, userId = request.UserId, sessionId = request.SessionId, houseId = request.HouseId, action = request.Action });
}).RequireAuthorization("trusted-service");
app.Run();

static TokenValidationParameters Validation(SecurityKey key, string audience) => new() { ValidIssuer = "Identity", ValidAudience = audience, IssuerSigningKey = key, ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256], RequireSignedTokens = true, RequireExpirationTime = true, ValidateIssuer = true, ValidateAudience = true, ValidateIssuerSigningKey = true, ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(10) };
static IResult InvalidCredentials() => Results.Problem(statusCode: 401, title: "Invalid credentials", extensions: new Dictionary<string, object?> { ["code"] = "invalid_credentials" });
static IResult SessionResult(Result<LoginResponse> result) => result is Result<LoginResponse>.Success success ? Results.Ok(success.Value) : InvalidCredentials();
public sealed record LoginRequest(string? Email, string? Password);
public sealed record RefreshRequest(string? RefreshToken);
public sealed record AssertionRequest(string GrantType, string Assertion);
public sealed record AuthorizationRequest(Guid UserId, Guid SessionId, Guid HouseId, string Action, Guid CorrelationId, string? ResourceId);
public sealed class UnavailableHandler : Microsoft.AspNetCore.Diagnostics.IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is not (SqlException or DbUpdateException)) return false;
        context.Response.StatusCode = 503;
        await Results.Problem(statusCode: 503, title: "Identity storage unavailable", extensions: new Dictionary<string, object?> { ["code"] = "unavailable", ["correlationId"] = context.TraceIdentifier }).ExecuteAsync(context);
        return true;
    }
}
public partial class Program { }
