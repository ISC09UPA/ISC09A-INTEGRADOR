using InvernaderoInteligente.Infrastructure;
using InvernaderoInteligente.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddInfrastructure(
    builder.Configuration.GetConnectionString("Invernadero") ?? "Data Source=invernadero.db");

var app = builder.Build();

// Crea la base de datos y las tablas si no existen (temporal, hasta usar migraciones)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<InvernaderoDbContext>();
    db.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "Invernadero API v1"));
}

app.MapControllers();
app.Run();
