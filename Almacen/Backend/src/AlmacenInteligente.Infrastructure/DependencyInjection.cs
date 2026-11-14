using AlmacenInteligente.Application.Abstracciones;
using AlmacenInteligente.Application.Notificaciones;
using AlmacenInteligente.Application.Consultas;
using AlmacenInteligente.Application.Usuarios;
using AlmacenInteligente.Application.Pdf;
using AlmacenInteligente.Infrastructure.Archivos;
using AlmacenInteligente.Infrastructure.Correos;
using AlmacenInteligente.Infrastructure.Mensajeria;
using AlmacenInteligente.Infrastructure.Pdf;
using AlmacenInteligente.Infrastructure.Persistencia;
using AlmacenInteligente.Infrastructure.Seguridad;
using Azure.Communication.Email;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AlmacenInteligente.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Adaptadores de salida: PostgreSQL, Identity, correo, archivos, PDF y el procesador del outbox.
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
        services.AddSingleton<IGeneradorPdf, GeneradorPdf>();
        AgregarArchivos(services, configuration);
        AgregarCorreo(services, configuration);

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

    /// <summary>Imágenes siempre en Azure Blob Storage (Azurite en desarrollo); sin respaldo en disco.</summary>
    private static void AgregarArchivos(IServiceCollection services, IConfiguration configuration)
    {
        var cadena = configuration["Archivos:Blob:CadenaConexion"];
        if (string.IsNullOrWhiteSpace(cadena))
            throw new InvalidOperationException(
                "Falta 'Archivos:Blob:CadenaConexion' (Azure Blob Storage; en desarrollo 'UseDevelopmentStorage=true' con Azurite).");
        var contenedor = new BlobContainerClient(cadena, configuration["Archivos:Blob:Contenedor"] ?? "productos");
        services.AddSingleton<IFileStorage>(new AlmacenamientoBlob(contenedor));
    }

    /// <summary>
    /// Correo: Azure Communication Services si hay "Correo:Azure:CadenaConexion"; si no SMTP si hay "Correo:Smtp:Host";
    /// si no, solo log. Los correos salen del outbox, que procesan en segundo plano tanto la API como la web.
    /// </summary>
    private static void AgregarCorreo(IServiceCollection services, IConfiguration configuration)
    {
        var c = configuration.GetSection("Correo");
        var opciones = new OpcionesCorreo(
            Remitente: c["Remitente"],
            NombreRemitente: c["NombreRemitente"] ?? configuration["Empresa:Nombre"] ?? "Almacén inteligente",
            CadenaAzure: c["Azure:CadenaConexion"],
            SmtpHost: c["Smtp:Host"],
            SmtpPuerto: c.GetValue("Smtp:Puerto", 25),
            SmtpUsuario: c["Smtp:Usuario"],
            SmtpContrasena: c["Smtp:Contrasena"],
            SmtpSsl: c.GetValue("Smtp:Ssl", false));
        services.AddSingleton(opciones);

        if (!string.IsNullOrWhiteSpace(opciones.CadenaAzure))
        {
            if (string.IsNullOrWhiteSpace(opciones.Remitente))
                throw new InvalidOperationException("Con Correo:Azure:CadenaConexion también hace falta Correo:Remitente (p. ej. DoNotReply@xxxx.azurecomm.net).");
            services.AddSingleton(new EmailClient(opciones.CadenaAzure));
            services.AddSingleton<IEmailSender, CorreoAzure>();
        }
        else if (!string.IsNullOrWhiteSpace(opciones.SmtpHost))
            services.AddSingleton<IEmailSender, CorreoSmtp>();
        else
            services.AddSingleton<IEmailSender, CorreoEnLog>();

        var n = configuration.GetSection("Notificaciones");
        services.AddSingleton(new OpcionesNotificaciones
        {
            UrlWeb = n["UrlWeb"],
            NombreEmpresa = configuration["Empresa:Nombre"] ?? "Almacén inteligente",
            ComprobanteVentaAlCliente = n.GetValue("ComprobanteVentaAlCliente", true),
            ComprobanteRecepcionAlProveedor = n.GetValue("ComprobanteRecepcionAlProveedor", true),
        });

        if (configuration.GetValue("Outbox:Habilitado", true))
            services.AddHostedService<ProcesadorOutbox>();
    }
}

internal sealed class RelojSistema : IReloj
{
    // Hora de negocio: centro de México (sin horario de verano desde 2022).
    private static readonly TimeZoneInfo ZonaMexico = TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");

    public DateTimeOffset Ahora => TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, ZonaMexico);

    public DateOnly Hoy => DateOnly.FromDateTime(Ahora.DateTime);
}
