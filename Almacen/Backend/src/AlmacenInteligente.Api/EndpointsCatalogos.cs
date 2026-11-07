using AlmacenInteligente.Application.Almacenes;
using AlmacenInteligente.Application.CasosDeUso;
using AlmacenInteligente.Application.Clientes;
using AlmacenInteligente.Application.Consultas;
using AlmacenInteligente.Application.Documentos;
using AlmacenInteligente.Application.Productos;
using AlmacenInteligente.Application.Proveedores;
using AlmacenInteligente.Application.Terceros;
using AlmacenInteligente.Application.Usuarios;
using AlmacenInteligente.Domain.Productos;

namespace AlmacenInteligente.Api;

// Cuerpos de las peticiones que no son un caso de uso completo (el id va en la ruta).
public sealed record CambioEstado(bool Activo);
public sealed record Motivo(string Texto);
public sealed record DatosProducto(string Descripcion, Guid UnidadId);
public sealed record DatosAlmacen(string Nombre, string Ubicacion);
public sealed record DatosUsuario(string Nombre, string Rol, bool Activo);
public sealed record NuevaContrasena(string Contrasena);
public sealed record StockMinimo(decimal Minimo);
public sealed record StockInicial(decimal Cantidad, decimal CostoUnitario);

/// <summary>Catálogos: clientes, proveedores, productos, unidades, almacenes, existencias y usuarios.</summary>
internal static class EndpointsCatalogos
{
    public static void MapCatalogos(this IEndpointRouteBuilder app)
    {
        MapClientes(app.MapGroup("/api/clientes").RequireAuthorization().WithTags("Clientes"));
        MapProveedores(app.MapGroup("/api/proveedores").RequireAuthorization().WithTags("Proveedores"));
        MapProductos(app.MapGroup("/api/productos").RequireAuthorization().WithTags("Productos"));
        MapAlmacenes(app.MapGroup("/api/almacenes").RequireAuthorization().WithTags("Almacenes y existencias"));
        MapUsuarios(app.MapGroup("/api/usuarios").RequireAuthorization().WithTags("Usuarios"));
    }

    private static void MapClientes(RouteGroupBuilder g)
    {
        g.MapGet("", (string? busqueda, bool? inactivos, int? pagina, int? tamano, IMediador m, CancellationToken ct) =>
            m.EnviarAsync(new ListarClientes(busqueda, inactivos ?? false, pagina ?? 1, tamano ?? 20), ct)).WithName("ListarClientes");
        g.MapGet("/activos", (string? texto, IMediador m, CancellationToken ct) =>
            m.EnviarAsync(new BuscarClientesActivos(texto), ct)).WithName("BuscarClientesActivos")
            .WithSummary("Para elegir cliente en una venta: código · nombre de los clientes activos.");
        g.MapGet("/{id:guid}", (Guid id, IMediador m, CancellationToken ct) => m.EnviarAsync(new ObtenerCliente(id), ct)).WithName("ObtenerCliente");
        g.MapPost("", async (DatosTercero datos, IMediador m, CancellationToken ct) =>
        {
            var creado = await m.EnviarAsync(new CrearCliente(datos), ct);
            return Results.Created($"/api/clientes/{creado.Id}", creado);
        }).WithName("CrearCliente");
        g.MapPut("/{id:guid}", (Guid id, DatosTercero datos, IMediador m, CancellationToken ct) =>
            m.EnviarAsync(new EditarCliente(id, datos), ct)).WithName("EditarCliente").WithSummary("El código no cambia aunque se envíe otro.");
        g.MapPatch("/{id:guid}/estado", (Guid id, CambioEstado c, IMediador m, CancellationToken ct) =>
            m.EnviarAsync(new CambiarEstadoCliente(id, c.Activo), ct)).WithName("CambiarEstadoCliente");
    }

    private static void MapProveedores(RouteGroupBuilder g)
    {
        g.MapGet("", (string? busqueda, bool? inactivos, int? pagina, int? tamano, IMediador m, CancellationToken ct) =>
            m.EnviarAsync(new ListarProveedores(busqueda, inactivos ?? false, pagina ?? 1, tamano ?? 20), ct)).WithName("ListarProveedores");
        g.MapGet("/activos", (string? texto, IMediador m, CancellationToken ct) =>
            m.EnviarAsync(new BuscarProveedoresActivos(texto), ct)).WithName("BuscarProveedoresActivos");
        g.MapGet("/{id:guid}", (Guid id, IMediador m, CancellationToken ct) => m.EnviarAsync(new ObtenerProveedor(id), ct)).WithName("ObtenerProveedor");
        g.MapPost("", async (DatosTercero datos, IMediador m, CancellationToken ct) =>
        {
            var creado = await m.EnviarAsync(new CrearProveedor(datos), ct);
            return Results.Created($"/api/proveedores/{creado.Id}", creado);
        }).WithName("CrearProveedor");
        g.MapPut("/{id:guid}", (Guid id, DatosTercero datos, IMediador m, CancellationToken ct) =>
            m.EnviarAsync(new EditarProveedor(id, datos), ct)).WithName("EditarProveedor");
        g.MapPatch("/{id:guid}/estado", (Guid id, CambioEstado c, IMediador m, CancellationToken ct) =>
            m.EnviarAsync(new CambiarEstadoProveedor(id, c.Activo), ct)).WithName("CambiarEstadoProveedor");
    }

    private static void MapProductos(RouteGroupBuilder g)
    {
        g.MapGet("", (string? busqueda, EstadoAprobacion? estado, bool? inactivos, bool? misAprobaciones, int? pagina, int? tamano,
                IMediador m, CancellationToken ct) =>
            m.EnviarAsync(new ListarProductos(new FiltroProductos(busqueda, estado, inactivos ?? false, misAprobaciones ?? false, pagina ?? 1, tamano ?? 20)), ct))
            .WithName("ListarProductos");
        g.MapGet("/disponibles", (string? texto, int? maximo, IMediador m, CancellationToken ct) =>
            m.EnviarAsync(new BuscarProductosDisponibles(texto, maximo ?? 20), ct)).WithName("BuscarProductosDisponibles")
            .WithSummary("Aprobados y activos, con la existencia por almacén. Para capturar ventas, recepciones y traspasos.");
        g.MapGet("/unidades", (IMediador m, CancellationToken ct) => m.EnviarAsync(new ListarUnidades(), ct)).WithName("ListarUnidades");
        g.MapPost("/unidades", (CrearUnidad s, IMediador m, CancellationToken ct) => m.EnviarAsync(s, ct)).WithName("CrearUnidad");
        g.MapGet("/gerentes", (IMediador m, CancellationToken ct) => m.EnviarAsync(new ListarGerentes(), ct)).WithName("ListarGerentes")
            .WithSummary("Gerentes activos a los que se puede enviar la aprobación de un producto.");
        g.MapGet("/{id:guid}", (Guid id, IMediador m, CancellationToken ct) => m.EnviarAsync(new ObtenerProducto(id), ct)).WithName("ObtenerProducto");
        g.MapPost("", async (CrearProducto s, IMediador m, CancellationToken ct) =>
        {
            var creado = await m.EnviarAsync(s, ct);
            return Results.Created($"/api/productos/{creado.Id}", creado);
        }).WithName("CrearProducto")
            .WithSummary("Si el usuario no puede aprobar, envía gerenteAprobadorId y el producto queda pendiente.");
        g.MapPut("/{id:guid}", (Guid id, DatosProducto d, IMediador m, CancellationToken ct) =>
            m.EnviarAsync(new EditarProducto(id, d.Descripcion, d.UnidadId), ct)).WithName("EditarProducto");
        g.MapPatch("/{id:guid}/estado", (Guid id, CambioEstado c, IMediador m, CancellationToken ct) =>
            m.EnviarAsync(new CambiarEstadoProducto(id, c.Activo), ct)).WithName("CambiarEstadoProducto");
        g.MapPost("/{id:guid}/imagen", async (Guid id, IFormFile imagen, IMediador m, CancellationToken ct) =>
        {
            await using var flujo = imagen.OpenReadStream();
            using var memoria = new MemoryStream();
            await flujo.CopyToAsync(memoria, ct);
            return await m.EnviarAsync(new SubirImagenProducto(id, imagen.FileName, imagen.ContentType, memoria.ToArray()), ct);
        }).DisableAntiforgery().WithName("SubirImagenProducto").WithSummary("multipart/form-data con el campo 'imagen' (JPG, PNG o WebP, máx. 2 MB).");
        g.MapPost("/{id:guid}/aprobar", (Guid id, IMediador m, CancellationToken ct) => m.EnviarAsync(new AprobarProducto(id), ct)).WithName("AprobarProducto");
        g.MapPost("/{id:guid}/rechazar", (Guid id, Motivo motivo, IMediador m, CancellationToken ct) =>
            m.EnviarAsync(new RechazarProducto(id, motivo.Texto), ct)).WithName("RechazarProducto");
    }

    private static void MapAlmacenes(RouteGroupBuilder g)
    {
        g.MapGet("", (bool? inactivos, IMediador m, CancellationToken ct) => m.EnviarAsync(new ListarAlmacenes(inactivos ?? false), ct)).WithName("ListarAlmacenes");
        g.MapPost("", (DatosAlmacen d, IMediador m, CancellationToken ct) => m.EnviarAsync(new CrearAlmacen(d.Nombre, d.Ubicacion), ct)).WithName("CrearAlmacen");
        g.MapPut("/{id:guid}", (Guid id, DatosAlmacen d, IMediador m, CancellationToken ct) =>
            m.EnviarAsync(new EditarAlmacen(id, d.Nombre, d.Ubicacion), ct)).WithName("EditarAlmacen");
        g.MapPatch("/{id:guid}/estado", (Guid id, CambioEstado c, IMediador m, CancellationToken ct) =>
            m.EnviarAsync(new CambiarEstadoAlmacen(id, c.Activo), ct)).WithName("CambiarEstadoAlmacen");

        g.MapGet("/existencias", (Guid? almacen, string? busqueda, bool? bajoMinimo, int? pagina, int? tamano, IMediador m, CancellationToken ct) =>
            m.EnviarAsync(new ListarExistencias(new FiltroExistencias(almacen, busqueda, bajoMinimo ?? false, pagina ?? 1, tamano ?? 50)), ct))
            .WithName("ListarExistencias");
        g.MapGet("/{id:guid}/kardex/{productoId:guid}", (Guid id, Guid productoId, DateOnly? desde, DateOnly? hasta, IMediador m, CancellationToken ct) =>
            m.EnviarAsync(new ConsultarKardex(id, productoId, desde, hasta), ct)).WithName("ConsultarKardex");
        g.MapPut("/{id:guid}/existencias/{productoId:guid}/minimo", (Guid id, Guid productoId, StockMinimo s, IMediador m, CancellationToken ct) =>
            m.EnviarAsync(new DefinirStockMinimo(id, productoId, s.Minimo), ct)).WithName("DefinirStockMinimo");
        g.MapPost("/{id:guid}/existencias/{productoId:guid}/inicial", (Guid id, Guid productoId, StockInicial s, IMediador m, CancellationToken ct) =>
            m.EnviarAsync(new RegistrarStockInicial(id, productoId, s.Cantidad, s.CostoUnitario), ct)).WithName("RegistrarStockInicial");
    }

    private static void MapUsuarios(RouteGroupBuilder g)
    {
        g.MapGet("", (string? rol, IMediador m, CancellationToken ct) => m.EnviarAsync(new ListarUsuarios(rol), ct)).WithName("ListarUsuarios");
        g.MapPost("", (CrearUsuario s, IMediador m, CancellationToken ct) => m.EnviarAsync(s, ct)).WithName("CrearUsuario")
            .WithSummary("rol: Superadmin, AgenteVentas, EncargadoRecepciones o Gerente.");
        g.MapPut("/{id:guid}", (Guid id, DatosUsuario d, IMediador m, CancellationToken ct) =>
            m.EnviarAsync(new ActualizarUsuario(id, d.Nombre, d.Rol, d.Activo), ct)).WithName("ActualizarUsuario");
        g.MapPost("/{id:guid}/contrasena", async (Guid id, NuevaContrasena c, IMediador m, CancellationToken ct) =>
        {
            await m.EnviarAsync(new RestablecerContrasena(id, c.Contrasena), ct);
            return Results.NoContent();
        }).WithName("RestablecerContrasena");
    }
}
