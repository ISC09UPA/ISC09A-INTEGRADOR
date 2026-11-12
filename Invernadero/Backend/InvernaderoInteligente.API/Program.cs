using InvernaderoInteligente.API.Middleware;
using InvernaderoInteligente.Infrastructure;
using InvernaderoInteligente.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);
var apiKey = builder.Configuration["TelemetriaApi:ApiKey"];
var connectionString = builder.Configuration.GetConnectionString("Invernadero");
var corsOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
var usarSqlServer = string.Equals(
    builder.Configuration["Database:Provider"],
    "SqlServer",
    StringComparison.OrdinalIgnoreCase);

if (!builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(apiKey))
    throw new InvalidOperationException("Falta la configuración TelemetriaApi__ApiKey para proteger la API en producción.");

if (string.IsNullOrWhiteSpace(connectionString) && !builder.Environment.IsDevelopment())
    throw new InvalidOperationException("Falta ConnectionStrings__Invernadero. Configura Azure SQL antes de iniciar la API en producción.");

if (!builder.Environment.IsDevelopment() && !usarSqlServer)
    throw new InvalidOperationException("Falta Database__Provider=SqlServer. Configura el proveedor de Azure SQL en producción.");

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy("MobileWeb", policy =>
    {
        if (corsOrigins.Length > 0)
            policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod();
    });
});

// Errores con formato estándar (ProblemDetails): 409 para duplicados, 500 para lo demás
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ManejadorErrores>();

builder.Services.AddInfrastructure(
    connectionString ?? "Data Source=invernadero.db",
    usarSqlServer);

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors("MobileWeb");
app.UseMiddleware<ApiKeyMiddleware>();

// SQLite local sigue usando EnsureCreated; SQL Server se actualiza con el paso de migración del despliegue.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<InvernaderoDbContext>();
    if (app.Environment.IsDevelopment() && !usarSqlServer)
        db.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "Invernadero API v1"));
}

app.MapControllers();
app.Run();
