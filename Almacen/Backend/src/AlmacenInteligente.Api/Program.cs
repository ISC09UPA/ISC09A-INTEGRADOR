using System.Text.Json.Serialization;
using AlmacenInteligente.Api;
using AlmacenInteligente.Application;
using AlmacenInteligente.Application.Abstracciones;
using AlmacenInteligente.Infrastructure;
using AlmacenInteligente.Infrastructure.Seguridad;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi(o => o.AddDocumentTransformer<DocumentoOpenApi>());
// Enums como texto ("Confirmado", "Ingreso"...), más claro para la app móvil que números.
// Números estrictos: así OpenAPI los declara como integer/number y Swagger no los confunde con texto.
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
});
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ManejadorErrores>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var jwt = OpcionesJwt.Desde(builder.Configuration)
    ?? throw new InvalidOperationException("Falta la sección 'Jwt' (Clave, Emisor, Audiencia).");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    // Los claims viajan con los mismos nombres que en la cookie de Blazor (incluidos los "permiso").
    o.MapInboundClaims = false;
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = jwt.Emisor,
        ValidAudience = jwt.Audiencia,
        IssuerSigningKey = jwt.LlaveFirma(),
        NameClaimType = System.Security.Claims.ClaimTypes.Name,
        RoleClaimType = System.Security.Claims.ClaimTypes.Role,
        ClockSkew = TimeSpan.FromMinutes(1),
    };
});
builder.Services.AddAuthorization();
// La app Expo en navegador (expo start --web) llama desde otro origen. El token viaja en Authorization, sin cookies,
// así que basta con permitir cualquier origen; Content-Disposition expuesto para el nombre de los PDF.
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("Content-Disposition")));

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors();

// Swagger en desarrollo; en otros ambientes solo si se activa con Swagger:Habilitado=true.
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Habilitado"))
{
    app.MapOpenApi();
    app.UseSwaggerUI(o =>
    {
        o.SwaggerEndpoint("/openapi/v1.json", "Almacén inteligente v1");
        o.RoutePrefix = "swagger";
        o.DocumentTitle = "Almacén inteligente · API";
        o.EnablePersistAuthorization();
        o.InjectJavascript("/swagger-token.js");
    });
    // Blazor abre /swagger#token=...: el fragmento no viaja al servidor; este script autoriza Swagger con él y lo borra de la URL.
    app.MapGet("/swagger-token.js", () => Results.Text("""
        (function () {
          var m = /[#&]token=([^&]+)/.exec(window.location.hash);
          if (!m) return;
          var token = decodeURIComponent(m[1]);
          history.replaceState(null, '', window.location.pathname + window.location.search);
          // Espera a que Swagger cargue el documento (con el esquema Bearer) antes de autorizar.
          var intentos = 0;
          var t = setInterval(function () {
            var ui = window.ui;
            if (ui && ui.specSelectors && ui.specSelectors.securityDefinitions()) {
              ui.authActions.authorize({ Bearer: { name: 'Bearer', schema: { type: 'http', scheme: 'bearer' }, value: token } });
              clearInterval(t);
            } else if (++intentos > 100) clearInterval(t);
          }, 200);
        })();
        """, "text/javascript")).ExcludeFromDescription();
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/salud", () => Results.Ok(new { estado = "ok" })).WithName("Salud").WithTags("Sistema").AllowAnonymous();
app.MapCuenta();
app.MapCatalogos();
app.MapOperacion();
app.MapPdf();

// Imágenes de productos (la ruta viene en imagenRuta del producto).
app.MapGet("/api/archivos/{**ruta}", async (string ruta, IFileStorage archivos, CancellationToken ct) =>
{
    var tipo = Path.GetExtension(ruta).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => null,
    };
    if (tipo is null)
        return Results.NotFound();
    try
    {
        return await archivos.AbrirAsync(ruta, ct) is { } flujo ? Results.Stream(flujo, tipo) : Results.NotFound();
    }
    catch (UnauthorizedAccessException)
    {
        return Results.NotFound();
    }
}).RequireAuthorization().WithTags("Productos").WithName("ImagenProducto");

app.Run();
