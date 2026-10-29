using AlmacenInteligente.Application.Abstracciones;
using AlmacenInteligente.Application.Consultas;
using AlmacenInteligente.Application.Usuarios;
using AlmacenInteligente.Application.Pdf;
using AlmacenInteligente.Infrastructure.Archivos;
using AlmacenInteligente.Infrastructure.Pdf;
using AlmacenInteligente.Infrastructure.Persistencia;
using AlmacenInteligente.Infrastructure.Seguridad;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AlmacenInteligente.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Adaptadores de salida. Correo, PDF y archivos se agregan aquí en las siguientes fases.
    /// Cada host registra su propio esquema de autenticación (cookie en Blazor, JWT en la API).
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var cadena = configuration.GetConnectionString("Almacen")
            ?? throw new InvalidOperationException("Falta la cadena de conexión 'ConnectionStrings:Almacen'.");

        services.AddDbContext<AlmacenDbContext>(o => o.UseNpgsql(cadena).UseSnakeCaseNamingConvention());
        services.AddScoped<IUnidadDeTrabajo>(sp => sp.GetRequiredService<AlmacenDbContext>());
        services.AddScoped<IClienteRepositorio, ClienteRepositorio>();
        services.AddScoped<IProveedorRepositorio, ProveedorRepositorio>();
        services.AddScoped<IUnidadRepositorio, UnidadRepositorio>();
        services.AddScoped<IProductoRepositorio, ProductoRepositorio>();
        services.AddScoped<IAlmacenRepositorio, AlmacenRepositorio>();
        services.AddScoped<IExistenciaRepositorio, ExistenciaRepositorio>();
        services.AddScoped<IRecepcionRepositorio, RecepcionRepositorio>();
        services.AddScoped<IVentaRepositorio, VentaRepositorio>();
        services.AddScoped<ITraspasoRepositorio, TraspasoRepositorio>();
        services.AddScoped<IConsultas, ConsultasEf>();
        services.AddScoped<Application.Movimientos.IConsultasMovimientos, ConsultasMovimientosEf>();
        services.AddScoped<Application.Finanzas.IConsultasFinanzas, ConsultasFinanzasEf>();
        services.AddSingleton<IFileStorage, AlmacenamientoLocal>();
        services.AddSingleton<IGeneradorPdf, GeneradorPdf>();

        services.AddIdentityCore<Usuario>(o =>
            {
                o.User.RequireUniqueEmail = true;
                o.Password.RequiredLength = 8;
                o.Password.RequireNonAlphanumeric = false;
                o.Lockout.MaxFailedAccessAttempts = 5;
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
            })
            .AddRoles<Rol>()
            .AddEntityFrameworkStores<AlmacenDbContext>()
            .AddSignInManager<GestorInicioSesion>()
            .AddDefaultTokenProviders()
            .AddClaimsPrincipalFactory<FabricaPrincipal>()
            .AddErrorDescriber<ErroresIdentidadEnEspanol>();
        services.AddScoped<IGestorUsuarios, GestorUsuarios>();
        services.AddScoped<IEmisorTokens, EmisorTokens>();

        services.AddHttpContextAccessor();
        services.AddScoped<IUsuarioActual, UsuarioActualHttp>();
        services.AddSingleton<IReloj, RelojSistema>();
        return services;
    }
}

internal sealed class RelojSistema : IReloj
{
    // Hora de negocio: centro de México (sin horario de verano desde 2022).
    private static readonly TimeZoneInfo ZonaMexico = TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");

    public DateTimeOffset Ahora => TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, ZonaMexico);

    public DateOnly Hoy => DateOnly.FromDateTime(Ahora.DateTime);
}
