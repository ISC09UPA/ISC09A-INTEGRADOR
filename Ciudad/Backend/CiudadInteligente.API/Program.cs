using System.Threading.RateLimiting;
using CiudadInteligente.API.Middleware;
using CiudadInteligente.API.Seguridad;
using CiudadInteligente.Application;
using CiudadInteligente.Application.Interfaces;
using CiudadInteligente.Domain.Enums;
using CiudadInteligente.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Capas de la arquitectura
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ---- Seguridad: login con token JWT + llave para la camara/simulador ----
var jwt = OpcionesJwt.Desde(builder.Configuration);
builder.Services.AddSingleton(jwt);
builder.Services.AddSingleton<IGeneradorToken, GeneradorTokenJwt>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opciones =>
    {
        // los claims se quedan con su nombre corto ("role", "name", "sub")
        opciones.MapInboundClaims = false;
        opciones.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Emisor,
            ValidateAudience = true,
            ValidAudience = jwt.Emisor,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(jwt.Clave),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = GeneradorTokenJwt.ClaimUsuario,
            RoleClaimType = GeneradorTokenJwt.ClaimRol
        };
    })
    .AddScheme<AuthenticationSchemeOptions, AutenticacionDispositivo>(AutenticacionDispositivo.Esquema, null);

builder.Services.AddAuthorizationBuilder()
    // todo pide sesion, salvo lo marcado con [AllowAnonymous] (el login)
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .AddPolicy(Politicas.SoloAdministrador, politica => politica.RequireRole(RolesUsuario.Administrador))
    .AddPolicy(Politicas.EnviarConteos, politica => politica
        .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, AutenticacionDispositivo.Esquema)
        .RequireRole(RolesUsuario.Dispositivo, RolesUsuario.Administrador));

// Maximo 10 intentos de login por minuto desde la misma IP (429 despues)
builder.Services.AddRateLimiter(opciones =>
{
    opciones.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    opciones.AddPolicy(Politicas.LimiteLogin, contexto => RateLimitPartition.GetFixedWindowLimiter(
        contexto.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

// Registro de peticiones: metodo, ruta, codigo y duracion. No guarda
// encabezados ni cuerpos, asi no se escriben tokens, X-Api-Key ni contrasenas.
builder.Services.AddHttpLogging(opciones =>
{
    opciones.LoggingFields = HttpLoggingFields.RequestMethod
        | HttpLoggingFields.RequestPath
        | HttpLoggingFields.ResponseStatusCode
        | HttpLoggingFields.Duration;
});

// Errores de negocio / hardware -> 400 / 401 / 404 / 503 con un mensaje que la app muestra
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ManejadorErrores>();

// La app del celular llama desde otra IP de la misma red WiFi
builder.Services.AddCors(opciones =>
{
    opciones.AddDefaultPolicy(politica => politica.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

// Crea las tablas SQLite si no existen (y los usuarios iniciales) y abre la conexion con el ESP32
app.Services.IniciarInfraestructura();

if (jwt.ClaveGenerada)
{
    app.Logger.LogWarning(
        "Jwt:Clave no esta configurada: se uso una clave temporal y las sesiones se pierden al reiniciar. " +
        "Configurala con: dotnet user-secrets set \"Jwt:Clave\" \"<32+ caracteres al azar>\"");
}

if (string.IsNullOrWhiteSpace(builder.Configuration["Dispositivos:ApiKey"]))
{
    app.Logger.LogWarning(
        "Dispositivos:ApiKey no esta configurada: la camara y el simulador no podran mandar conteos. " +
        "Configurala con: dotnet user-secrets set \"Dispositivos:ApiKey\" \"<llave>\"");
}

app.UseExceptionHandler();
app.UseHttpLogging();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

// Sin UseHttpsRedirection: en la red local el celular y la Raspberry Pi
// hablan por http://<IP-de-la-PC>:5240 (ver Properties/launchSettings.json).

app.UseCors();

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();

app.Run();
