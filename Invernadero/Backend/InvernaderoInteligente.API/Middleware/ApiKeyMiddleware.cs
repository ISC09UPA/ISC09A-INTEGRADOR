using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace InvernaderoInteligente.API.Middleware;

public sealed class ApiKeyMiddleware(
    RequestDelegate next,
    IConfiguration configuration,
    IProblemDetailsService problemDetails)
{
    private const string HeaderApiKey = "X-Api-Key";
    private readonly string? _apiKey = configuration["TelemetriaApi:ApiKey"];

    public async Task InvokeAsync(HttpContext http)
    {
        if (RequiereApiKey(http.Request) && !EsApiKeyValida(http.Request))
        {
            http.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await problemDetails.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = http,
                ProblemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "No autorizado",
                    Detail = "Se requiere una API key válida."
                }
            });
            return;
        }

        await next(http);
    }

    private static bool RequiereApiKey(HttpRequest request) =>
        HttpMethods.IsPost(request.Method) &&
        (request.Path.StartsWithSegments("/api/Lecturas") ||
         request.Path.StartsWithSegments("/api/Riegos"));

    private bool EsApiKeyValida(HttpRequest request)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            return true;

        var recibidas = request.Headers[HeaderApiKey];
        if (recibidas.Count != 1 || string.IsNullOrEmpty(recibidas[0]))
            return false;

        var esperada = Encoding.UTF8.GetBytes(_apiKey);
        var recibida = Encoding.UTF8.GetBytes(recibidas[0]!);

        return esperada.Length == recibida.Length &&
            CryptographicOperations.FixedTimeEquals(esperada, recibida);
    }
}
