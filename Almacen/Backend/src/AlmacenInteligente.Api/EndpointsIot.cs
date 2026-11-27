using AlmacenInteligente.Application.CasosDeUso;
using AlmacenInteligente.Application.Iot;

namespace AlmacenInteligente.Api;

public sealed record DatosModoInteligente(Guid AlmacenId);

/// <summary>Embebido IoT: fotos de rostro, modelo de reconocimiento, bitácora de accesos y modo inteligente de venta.</summary>
internal static class EndpointsIot
{
    /// <summary>Política de rate limiting para crear solicitudes de inicio de sesión (ver Program.cs).</summary>
    public const string LimiteInicioSesionIot = "inicio-sesion-iot";

    public static void MapIot(this IEndpointRouteBuilder app)
    {
        var fotos = app.MapGroup("/api/usuarios/{id:guid}/fotos").RequireAuthorization().WithTags("IoT · Reconocimiento facial");
        fotos.MapGet("", (Guid id, IMediador m, CancellationToken ct) => m.EnviarAsync(new ListarFotosRostro(id), ct))
            .WithName("ListarFotosRostro")
            .WithSummary("Fotos de rostro del usuario. La imagen se descarga en /api/archivos/{ruta}.");
        fotos.MapPost("", async (Guid id, IFormFile foto, IMediador m, CancellationToken ct) =>
        {
            await using var flujo = foto.OpenReadStream();
            using var memoria = new MemoryStream();
            await flujo.CopyToAsync(memoria, ct);
            return await m.EnviarAsync(new SubirFotoRostro(id, foto.FileName, foto.ContentType, memoria.ToArray()), ct);
        }).DisableAntiforgery().WithName("SubirFotoRostro")
            .WithSummary("multipart/form-data con el campo 'foto' (JPG o PNG, máx. 4 MB). Se necesitan al menos 5 por usuario para entrenar.");
        fotos.MapDelete("/{fotoId:guid}", async (Guid id, Guid fotoId, IMediador m, CancellationToken ct) =>
        {
            await m.EnviarAsync(new EliminarFotoRostro(id, fotoId), ct);
            return Results.NoContent();
        }).WithName("EliminarFotoRostro");

        // Inicio de sesión de la app móvil con el embebido: anónimo porque todavía no hay token.
        var login = app.MapGroup("/api/iot/login").AllowAnonymous().WithTags("IoT · Inicio de sesión con el embebido");
        login.MapPost("", async (ServicioInicioSesionIot servicio, CancellationToken ct) =>
                Results.Ok(await servicio.CrearAsync(ct)))
            .RequireRateLimiting(LimiteInicioSesionIot)
            .WithName("CrearInicioSesionIot")
            .WithSummary("Crea una solicitud que dura 60 s: la persona pasa su tarjeta y su rostro por el lector. 409 si ya hay otra pendiente; 429 si se abusa.");
        login.MapGet("/{id:guid}", (Guid id, ServicioInicioSesionIot servicio, CancellationToken ct) => servicio.ConsultarAsync(id, ct))
            .WithName("ConsultarInicioSesionIot")
            .WithSummary("Sondeo (cada ~1 s). estado: pendiente, completada, negada, expirada, consumida o cancelada. Con completada trae token (el mismo de /api/cuenta/token) una sola vez.");
        login.MapDelete("/{id:guid}", async (Guid id, ServicioInicioSesionIot servicio, CancellationToken ct) =>
        {
            await servicio.CancelarAsync(id, ct);
            return Results.NoContent();
        }).WithName("CancelarInicioSesionIot");

        var iot = app.MapGroup("/api/iot").RequireAuthorization().WithTags("IoT");
        iot.MapPost("/modelo/entrenar", (IMediador m, CancellationToken ct) => m.EnviarAsync(new EntrenarModeloRostros(), ct))
            .WithName("EntrenarModeloRostros")
            .WithSummary("Entrena el modelo de Custom Vision, espera a que termine y lo publica. Requiere 2 usuarios o más con 5 fotos o más.");
        iot.MapGet("/accesos", (bool? permitido, DateOnly? desde, DateOnly? hasta, int? pagina, int? tamano, IMediador m, CancellationToken ct) =>
                m.EnviarAsync(new ListarAccesosIot(new FiltroAccesosIot(permitido, desde, hasta, pagina ?? 1, tamano ?? 50)), ct))
            .WithName("ListarAccesosIot")
            .WithSummary("Bitácora de accesos con tarjeta NFC + rostro, del más reciente al más antiguo.");

        var venta = iot.MapGroup("/venta").WithTags("IoT · Modo inteligente de venta");
        venta.MapGet("", async (IMediador m, CancellationToken ct) =>
                await m.EnviarAsync(new ObtenerModoInteligente(), ct) is { } sesion ? Results.Ok(sesion) : Results.NoContent())
            .WithName("ObtenerModoInteligente")
            .WithSummary("Quién tiene el lector NFC de ventas. 204 si está libre.");
        venta.MapPost("/tomar", (DatosModoInteligente d, IMediador m, CancellationToken ct) => m.EnviarAsync(new TomarModoInteligente(d.AlmacenId), ct))
            .WithName("TomarModoInteligente")
            .WithSummary("Toma el lector para el usuario actual con un almacén por defecto. 409 si otro usuario lo tiene vigente.");
        venta.MapPost("/latido", (IMediador m, CancellationToken ct) => m.EnviarAsync(new LatidoModoInteligente(), ct))
            .WithName("LatidoModoInteligente")
            .WithSummary("Mantiene el candado; envíalo cada ~15 s. Vence sin latidos tras Iot:TimeoutSesionMinutos.");
        venta.MapPost("/liberar", async (IMediador m, CancellationToken ct) =>
        {
            await m.EnviarAsync(new LiberarModoInteligente(), ct);
            return Results.NoContent();
        }).WithName("LiberarModoInteligente");
        venta.MapPost("/escaneos", (IMediador m, CancellationToken ct) => m.EnviarAsync(new TomarEscaneosPendientes(), ct))
            .WithName("TomarEscaneosPendientes")
            .WithSummary("Productos escaneados desde la última consulta (cada uno se entrega una sola vez). Agrégalos como líneas de cantidad 1.");
    }
}
