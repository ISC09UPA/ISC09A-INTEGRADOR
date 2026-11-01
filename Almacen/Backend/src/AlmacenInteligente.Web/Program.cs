using AlmacenInteligente.Application;
using AlmacenInteligente.Application.Abstracciones;
using AlmacenInteligente.Application.Seguridad;
using AlmacenInteligente.Infrastructure;
using AlmacenInteligente.Infrastructure.Persistencia;
using AlmacenInteligente.Infrastructure.Seguridad;
using AlmacenInteligente.Web.Components;
using AlmacenInteligente.Web.Seguridad;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.Replace(ServiceDescriptor.Scoped<IUsuarioActual, UsuarioActualPrincipal>());
builder.Services.AddScoped<Operaciones>();
builder.Services.AddHttpClient();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, RevalidacionSesion>();
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/cuenta/ingresar";
    o.AccessDeniedPath = "/cuenta/acceso-denegado";
    o.ExpireTimeSpan = TimeSpan.FromHours(10);
    o.SlidingExpiration = true;
});
builder.Services.AddAuthorization(o =>
{
    // Una política por permiso: las páginas usan [Authorize(Policy = Permisos.X.Y)].
    foreach (var permiso in Permisos.Todos)
        o.AddPolicy(permiso, p => p.RequireClaim(UsuarioActualPrincipal.ClaimPermiso, permiso));
});

var app = builder.Build();

// Migraciones y semilla: siempre en desarrollo; en otros ambientes solo con BaseDeDatos:InicializarAlArrancar=true.
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("BaseDeDatos:InicializarAlArrancar"))
{
    await InicializadorBaseDeDatos.InicializarAsync(app.Services);
}
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

// Explícitos para que también corran en las páginas re-ejecutadas por UseStatusCodePagesWithReExecute.
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Imágenes de productos: solo con sesión iniciada.
app.MapGet("/archivos/{**ruta}", async (string ruta, IFileStorage archivos, CancellationToken ct) =>
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
}).RequireAuthorization();

app.MapPdf();

app.MapPost("/cuenta/salir", async (SignInManager<Usuario> sesion) =>
{
    await sesion.SignOutAsync();
    return Results.LocalRedirect("~/cuenta/ingresar");
});

app.Run();
