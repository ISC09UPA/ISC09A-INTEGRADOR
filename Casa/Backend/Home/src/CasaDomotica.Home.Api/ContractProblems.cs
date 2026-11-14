using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
public static class ContractProblems
{
    public static void Configure(ProblemDetailsOptions options) => options.CustomizeProblemDetails = context =>
    {
        if (!context.HttpContext.Request.Path.StartsWithSegments("/api/v1")) return;
        var raw = context.ProblemDetails.Extensions.TryGetValue("code", out var rawValue) ? rawValue?.ToString() : null;
        var correlation = context.ProblemDetails.Extensions.TryGetValue("correlationId", out var correlationValue) ? correlationValue?.ToString() : null;
        var status = context.ProblemDetails.Status ?? 500;
        var code = raw switch
        {
            "invalidCredential" or "invalid_credentials" => "invalidCredential",
            "sessionRevoked" => "sessionRevoked",
            _ => status switch { 400 or 422 => "badRequest", 401 => "invalidCredential", 403 => "forbidden", 404 => "notFound", 409 => "conflict", 429 => "rateLimited", _ => "serviceUnavailable" }
        };
        context.ProblemDetails.Extensions.Clear();
        context.ProblemDetails.Extensions["code"] = code;
        context.ProblemDetails.Extensions["correlationId"] = Guid.TryParse(correlation, out var id) ? id.ToString() : context.HttpContext.TraceIdentifier;
    };
    public static async Task Correlate(HttpContext context, RequestDelegate next)
    {
        context.TraceIdentifier = Guid.NewGuid().ToString();
        context.Response.Headers["X-Correlation-ID"] = context.TraceIdentifier;
        await next(context);
    }
    public static async Task WriteStatus(StatusCodeContext context)
    {
        if (!context.HttpContext.Request.Path.StartsWithSegments("/api/v1")) return;
        await Results.Problem(statusCode: context.HttpContext.Response.StatusCode).ExecuteAsync(context.HttpContext);
    }
}
