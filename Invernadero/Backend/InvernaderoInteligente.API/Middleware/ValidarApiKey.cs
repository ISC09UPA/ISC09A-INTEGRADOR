using System.Security.Cryptography;
using System.Text;

namespace InvernaderoInteligente.API.Middleware;

/// <summary>
/// Protege las escrituras de la API con el header <c>X-Api-Key</c> (pendiente #4 del contrato).
/// - Solo aplica a POST/PUT/PATCH/DELETE bajo /api; los GET quedan abiertos para la app móvil.
/// - Si <c>Seguridad:ApiKey</c> está vacío (local), no valida nada.
/// En App Service se configura con el app setting <c>Seguridad__ApiKey</c>; la Function manda
/// el mismo valor en <c>TelemetriaApi__ApiKey</c>.
/// </summary>
public class ValidarApiKey(RequestDelegate next, IConfiguration config, ILogger<ValidarApiKey> logger)
{
    public const string Header = "X-Api-Key";

    private readonly byte[]? _llave = string.IsNullOrWhiteSpace(config["Seguridad:ApiKey"])
        ? null
        : Encoding.UTF8.GetBytes(config["Seguridad:ApiKey"]!);

    public async Task InvokeAsync(HttpContext http)
    {
        if (_llave is null || !EsEscritura(http.Request))
        {
            await next(http);
            return;
        }

        var recibida = http.Request.Headers[Header].ToString();
        if (string.IsNullOrEmpty(recibida) ||
            !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(recibida), _llave))
        {
            logger.LogWarning("{Metodo} {Ruta} rechazado: falta o no coincide {Header}",
                http.Request.Method, http.Request.Path, Header);
            http.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await http.Response.WriteAsJsonAsync(new
            {
                status = 401,
                title = "No autorizado",
                detail = $"Falta el header {Header} o no es válido."
            });
            return;
        }

        await next(http);
    }

    private static bool EsEscritura(HttpRequest r) =>
        r.Path.StartsWithSegments("/api") &&
        (HttpMethods.IsPost(r.Method) || HttpMethods.IsPut(r.Method) ||
         HttpMethods.IsPatch(r.Method) || HttpMethods.IsDelete(r.Method));
}
