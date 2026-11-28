using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace AlmacenInteligente.Api;

/// <summary>Documento OpenAPI con el esquema Bearer, para que Swagger UI muestre el botón Authorize.</summary>
internal sealed class DocumentoOpenApi : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument documento, OpenApiDocumentTransformerContext contexto, CancellationToken ct)
    {
        documento.Info.Title = "Almacén inteligente · API";
        documento.Info.Version = "v1";
        documento.Info.Description =
            "Los mismos casos de uso y permisos que la web. Obtén un token en POST /api/cuenta/token y pégalo en Authorize. " +
            "Errores en formato ProblemDetails: 401 sin sesión, 403 sin permiso, 404 no existe, 409 regla de negocio, 422 datos inválidos.";

        documento.Components ??= new OpenApiComponents();
        documento.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        documento.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Token de POST /api/cuenta/token (sin la palabra Bearer).",
        };
        documento.Security ??= [];
        documento.Security.Add(new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", documento)] = [] });
        return Task.CompletedTask;
    }
}
