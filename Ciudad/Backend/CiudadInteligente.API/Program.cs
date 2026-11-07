using CiudadInteligente.API.Middleware;
using CiudadInteligente.Application;
using CiudadInteligente.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Capas de la arquitectura
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Errores de negocio / hardware -> 400 / 503 con un mensaje que la app muestra
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ManejadorErrores>();

// La app del celular llama desde otra IP de la misma red WiFi
builder.Services.AddCors(opciones =>
{
    opciones.AddDefaultPolicy(politica => politica.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

// Crea las tablas SQLite si no existen y abre la conexion con el ESP32
app.Services.IniciarInfraestructura();

app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Sin UseHttpsRedirection: en la red local el celular y la Raspberry Pi
// hablan por http://<IP-de-la-PC>:5240 (ver Properties/launchSettings.json).

app.UseCors();

app.UseAuthorization();

app.MapControllers();

app.Run();
