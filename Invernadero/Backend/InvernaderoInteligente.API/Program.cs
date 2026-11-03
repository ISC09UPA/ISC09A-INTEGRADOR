using InvernaderoInteligente.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddInfrastructure(
    builder.Configuration.GetConnectionString("Invernadero") ?? "Data Source=invernadero.db");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "Invernadero API v1"));
}

app.MapControllers();
app.Run();
