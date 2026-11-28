using System.Reflection;
using AlmacenInteligente.Application.Abstracciones;
using AlmacenInteligente.Application.CasosDeUso;
using AlmacenInteligente.Application.Consultas;
using AlmacenInteligente.Application.Iot;
using AlmacenInteligente.Application.Seguridad;
using AlmacenInteligente.Application.Terceros;
using AlmacenInteligente.Application.Usuarios;
using AlmacenInteligente.Domain.Almacenes;
using AlmacenInteligente.Domain.Common;
using AlmacenInteligente.Domain.Iot;
using AlmacenInteligente.Domain.Productos;
using AlmacenInteligente.Domain.Ventas;
using Microsoft.Extensions.DependencyInjection;

namespace AlmacenInteligente.Application.Tests;

/// <summary>Dobles en memoria para los casos de uso del embebido.</summary>
internal sealed class RelojFijo : IReloj
{
    public DateTimeOffset Ahora { get; set; } = new(2026, 11, 6, 10, 0, 0, TimeSpan.FromHours(-6));
    public DateOnly Hoy => DateOnly.FromDateTime(Ahora.Date);
}

internal sealed class UsuarioDePrueba(Guid id, string rol) : IUsuarioActual
{
    public Guid? Id { get; } = id;
    public bool EstaAutenticado => true;
    public bool TieneRol(string r) => r == rol;
    public bool TienePermiso(string permiso) => MatrizPermisos.PorDefecto[rol].Contains(permiso);
}

internal sealed class GestorUsuariosEnMemoria : IGestorUsuarios
{
    public List<UsuarioDto> Usuarios { get; } = [];

    public Task<IReadOnlyList<UsuarioDto>> ListarAsync(string? rol, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<UsuarioDto>>(Usuarios.Where(u => rol is null || u.Rol == rol).ToList());

    public Task<UsuarioDto?> ObtenerAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Usuarios.SingleOrDefault(u => u.Id == id));

    public Task<IReadOnlyList<UsuarioDto>> ListarConPermisoAsync(string permiso, CancellationToken ct = default) => ListarAsync(null, ct);

    public Task<UsuarioDto> CrearAsync(string nombre, string correo, string rol, string contrasena, string? codigoNfc, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<UsuarioDto> ActualizarAsync(Guid id, string nombre, string rol, bool activo, string? codigoNfc, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task RestablecerContrasenaAsync(Guid id, string contrasena, CancellationToken ct = default) => throw new NotSupportedException();

    public Task<UsuarioDto?> ObtenerPorNfcAsync(string codigoNfc, CancellationToken ct = default) =>
        Task.FromResult(Usuarios.SingleOrDefault(u => u.CodigoNfc == codigoNfc));

    public Task<bool> ExisteNfcAsync(string codigoNfc, Guid? excepto, CancellationToken ct = default) =>
        Task.FromResult(Usuarios.Any(u => u.CodigoNfc == codigoNfc && u.Id != excepto));
}

/// <summary>Mismas reglas que la sentencia SQL del adaptador real, usando el modelo de dominio.</summary>
internal sealed class CandadoEnMemoria(TimeSpan limite) : ICandadoVentaInteligente
{
    public SesionVentaInteligente Sesion { get; } = new();
    public List<EscaneoIot> Escaneos { get; } = [];

    public Task<SesionVentaInteligente?> ObtenerAsync(CancellationToken ct = default) =>
        Task.FromResult<SesionVentaInteligente?>(Sesion.UsuarioId is null ? null : Sesion);

    public Task<bool> IntentarTomarAsync(Guid usuarioId, Guid almacenId, DateTimeOffset ahora, DateTimeOffset venceAntesDe, CancellationToken ct = default)
    {
        if (!Sesion.PuedeTomar(usuarioId, ahora, limite))
            return Task.FromResult(false);
        Sesion.Tomar(usuarioId, almacenId, ahora, limite);
        return Task.FromResult(true);
    }

    public Task<bool> LatidoAsync(Guid usuarioId, DateTimeOffset ahora, CancellationToken ct = default) =>
        Task.FromResult(Sesion.Latido(usuarioId, ahora));

    public Task LiberarAsync(Guid usuarioId, CancellationToken ct = default)
    {
        Sesion.Liberar(usuarioId);
        return Task.CompletedTask;
    }

    public Task<RegistroEscaneo> RegistrarEscaneoAsync(string uid, string familia, Guid? productoId, string? rechazo,
        DateTimeOffset ahora, DateTimeOffset venceAntesDe, CancellationToken ct = default)
    {
        if (!Sesion.EstaVigente(ahora, limite))
            return Task.FromResult(RegistroEscaneo.SinSesion);
        if (Escaneos.Any(e => !e.Procesado && e.SesionId == Sesion.SesionId && e.Uid == uid))
            return Task.FromResult(RegistroEscaneo.Repetido);
        Escaneos.Add(new EscaneoIot(Sesion.SesionId!.Value, uid, familia, productoId, ahora, rechazo));
        return Task.FromResult(RegistroEscaneo.Registrado);
    }

    public Task<IReadOnlyList<EscaneoIot>> TomarEscaneosPendientesAsync(Guid usuarioId, CancellationToken ct = default)
    {
        var pendientes = Escaneos.Where(e => !e.Procesado && e.SesionId == Sesion.SesionId && Sesion.UsuarioId == usuarioId).ToList();
        pendientes.ForEach(e => e.MarcarProcesado());
        return Task.FromResult<IReadOnlyList<EscaneoIot>>(pendientes);
    }
}

internal sealed class AlmacenesEnMemoria : IAlmacenRepositorio
{
    public List<Almacen> Almacenes { get; } = [];
    public Task<Almacen?> ObtenerAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Almacenes.SingleOrDefault(a => a.Id == id));
    public void Agregar(Almacen entidad) => Almacenes.Add(entidad);
    public Task<bool> ExisteNombreAsync(string nombre, Guid? excepto, CancellationToken ct = default) => Task.FromResult(false);
}

/// <summary>Implementación vacía de una interfaz grande que la prueba no usa.</summary>
public class SinUso<T> : DispatchProxy where T : class
{
    public static T Crear() => DispatchProxy.Create<T, SinUso<T>>();

    protected override object? Invoke(MethodInfo? metodo, object?[]? argumentos) =>
        throw new NotSupportedException($"{typeof(T).Name}.{metodo?.Name} no se usa en esta prueba.");
}

public class VentaInteligenteTests
{
    private static readonly OpcionesIot Opciones = new() { TimeoutSesionMinutos = 2 };
    private static readonly Guid Ana = Guid.NewGuid();
    private static readonly Guid Beto = Guid.NewGuid();

    private sealed record Contexto(CandadoEnMemoria Candado, RelojFijo Reloj, Almacen Almacen, Func<Guid, IMediador> Como);

    /// <summary>Ana es agente de ventas y Beto gerente; comparten el mismo candado, reloj y catálogos.</summary>
    private static Contexto Crear()
    {
        var candado = new CandadoEnMemoria(Opciones.TimeoutSesion);
        var reloj = new RelojFijo();
        var almacenes = new AlmacenesEnMemoria();
        var almacen = new Almacen("Central", "CDMX", new Auditoria(Ana, reloj.Ahora));
        almacenes.Agregar(almacen);
        var usuarios = new GestorUsuariosEnMemoria();
        usuarios.Usuarios.Add(new UsuarioDto(Ana, "Ana López", "ana@x.mx", Roles.AgenteVentas, true));
        usuarios.Usuarios.Add(new UsuarioDto(Beto, "Beto Ruiz", "beto@x.mx", Roles.Gerente, true));

        IMediador Como(Guid id) => new ServiceCollection()
            .AddApplication()
            .AddSingleton<IUsuarioActual>(new UsuarioDePrueba(id, id == Ana ? Roles.AgenteVentas : Roles.Gerente))
            .AddSingleton<IReloj>(reloj)
            .AddSingleton(Opciones)
            .AddSingleton<ICandadoVentaInteligente>(candado)
            .AddSingleton<IGestorUsuarios>(usuarios)
            .AddSingleton<IAlmacenRepositorio>(almacenes)
            .AddSingleton(SinUso<IConsultas>.Crear())
            .BuildServiceProvider()
            .CreateScope().ServiceProvider.GetRequiredService<IMediador>();

        return new Contexto(candado, reloj, almacen, Como);
    }

    [Fact]
    public async Task Si_otro_lo_tiene_vigente_el_error_dice_quien()
    {
        var c = Crear();
        await c.Como(Ana).EnviarAsync(new TomarModoInteligente(c.Almacen.Id));

        c.Reloj.Ahora = c.Reloj.Ahora.AddMinutes(1);
        var error = await Assert.ThrowsAsync<DomainException>(() => c.Como(Beto).EnviarAsync(new TomarModoInteligente(c.Almacen.Id)));

        Assert.Equal("El modo inteligente lo está usando Ana López.", error.Message);
        Assert.Equal(Ana, c.Candado.Sesion.UsuarioId);
    }

    [Fact]
    public async Task Al_vencer_el_latido_otro_usuario_lo_toma()
    {
        var c = Crear();
        await c.Como(Ana).EnviarAsync(new TomarModoInteligente(c.Almacen.Id));

        c.Reloj.Ahora = c.Reloj.Ahora.AddMinutes(3);
        var sesion = await c.Como(Beto).EnviarAsync(new TomarModoInteligente(c.Almacen.Id));

        Assert.Equal(Beto, sesion.UsuarioId);
        Assert.Equal("Central", sesion.Almacen);
        Assert.True(sesion.EsMia);
        var perdido = await Assert.ThrowsAsync<DomainException>(() => c.Como(Ana).EnviarAsync(new LatidoModoInteligente()));
        Assert.Contains("Ya no tienes el modo inteligente", perdido.Message);
    }

    [Fact]
    public async Task Liberar_deja_el_lector_libre_para_otro()
    {
        var c = Crear();
        await c.Como(Ana).EnviarAsync(new TomarModoInteligente(c.Almacen.Id));
        await c.Como(Ana).EnviarAsync(new LiberarModoInteligente());

        Assert.Null(await c.Como(Beto).EnviarAsync(new ObtenerModoInteligente()));
        var sesion = await c.Como(Beto).EnviarAsync(new TomarModoInteligente(c.Almacen.Id));
        Assert.Equal(Beto, sesion.UsuarioId);
    }

    [Fact]
    public async Task Encargado_de_recepciones_no_puede_usar_el_modo_inteligente()
    {
        var c = Crear();
        var scope = new ServiceCollection().AddApplication()
            .AddSingleton<IUsuarioActual>(new UsuarioDePrueba(Guid.NewGuid(), Roles.EncargadoRecepciones))
            .BuildServiceProvider().CreateScope();
        await Assert.ThrowsAsync<AccesoDenegadoException>(() =>
            scope.ServiceProvider.GetRequiredService<IMediador>().EnviarAsync(new TomarModoInteligente(c.Almacen.Id)));
    }
}

internal sealed class ProductosEnMemoria : IProductoRepositorio
{
    public List<Producto> Productos { get; } = [];
    public Task<Producto?> ObtenerAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Productos.SingleOrDefault(p => p.Id == id));
    public void Agregar(Producto entidad) => Productos.Add(entidad);
    public Task<bool> ExisteReferenciaAsync(string referencia, CancellationToken ct = default) => Task.FromResult(Productos.Any(p => p.Referencia == referencia));
    public Task<bool> ExisteCodigoNfcAsync(string codigoNfc, Guid? excepto, CancellationToken ct = default) => Task.FromResult(false);
    public Task<Producto?> ObtenerPorCodigoNfcAsync(string codigoNfc, CancellationToken ct = default) =>
        Task.FromResult(Productos.SingleOrDefault(p => p.CodigoNfc == codigoNfc));
    public Task<IReadOnlyDictionary<Guid, Producto>> ObtenerVariosAsync(IEnumerable<Guid> ids, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, Producto>>(Productos.Where(p => ids.Contains(p.Id)).ToDictionary(p => p.Id));
}

internal sealed class VentasEnMemoria : IVentaRepositorio
{
    public HashSet<string> Vendidas { get; } = [];
    public Task<Venta?> ObtenerAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Venta?>(null);
    public void Agregar(Venta entidad) => throw new NotSupportedException();
    public Task<IReadOnlyList<string>> PiezasVendidasAsync(IReadOnlyCollection<string> uids, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<string>>(uids.Where(Vendidas.Contains).ToList());
}

public class ServicioEscaneosIotTests
{
    private static readonly OpcionesIot Opciones = new() { TimeoutSesionMinutos = 2 };
    private static readonly Guid Ana = Guid.NewGuid();

    private sealed record Contexto(ServicioEscaneosIot Servicio, CandadoEnMemoria Candado, ProductosEnMemoria Productos, VentasEnMemoria Ventas);

    private static Contexto Crear(bool conSesion = true)
    {
        var reloj = new RelojFijo();
        var candado = new CandadoEnMemoria(Opciones.TimeoutSesion);
        if (conSesion)
            candado.Sesion.Tomar(Ana, Guid.NewGuid(), reloj.Ahora, Opciones.TimeoutSesion);
        var productos = new ProductosEnMemoria();
        var tornillo = Producto.CrearAprobado("TOR-001", "Tornillo", new Unidad("PZA", "Pieza", permiteFracciones: false), new Auditoria(Ana, reloj.Ahora));
        tornillo.AsignarCodigoNfc("A1B2C3");
        productos.Agregar(tornillo);
        var ventas = new VentasEnMemoria();
        return new Contexto(new ServicioEscaneosIot(productos, ventas, candado, reloj, Opciones), candado, productos, ventas);
    }

    [Fact]
    public async Task Pieza_nueva_de_una_familia_del_catalogo_se_agrega_con_su_uid()
    {
        var c = Crear();

        var (resultado, productoId) = await c.Servicio.RegistrarAsync("53:2b:84:e5:03:00:01", "a1:b2:c3");

        Assert.Equal(ResultadoEscaneo.Registrado, resultado);
        var escaneo = Assert.Single(c.Candado.Escaneos);
        Assert.Equal("532B84E5030001", escaneo.Uid);
        Assert.Equal("A1B2C3", escaneo.Familia);
        Assert.Equal(c.Productos.Productos[0].Id, productoId);
        Assert.True(escaneo.Aceptado);
    }

    [Fact]
    public async Task Pieza_ya_vendida_se_rechaza_y_el_agente_ve_el_motivo()
    {
        var c = Crear();
        c.Ventas.Vendidas.Add("532B84E5030001");

        var (resultado, _) = await c.Servicio.RegistrarAsync("532B84E5030001", "A1B2C3");

        Assert.Equal(ResultadoEscaneo.PiezaYaVendida, resultado);
        var escaneo = Assert.Single(c.Candado.Escaneos);
        Assert.False(escaneo.Aceptado);
        Assert.Contains("ya se vendió", escaneo.Rechazo);
    }

    [Fact]
    public async Task Familia_desconocida_se_rechaza()
    {
        var c = Crear();
        var (resultado, productoId) = await c.Servicio.RegistrarAsync("532B84E5030001", "FFFF99");
        Assert.Equal(ResultadoEscaneo.ProductoNoEncontrado, resultado);
        Assert.Null(productoId);
        Assert.Contains("FFFF99", Assert.Single(c.Candado.Escaneos).Rechazo);
    }

    [Fact]
    public async Task Familia_que_no_es_hexadecimal_se_rechaza_con_motivo()
    {
        var c = Crear();
        var (resultado, _) = await c.Servicio.RegistrarAsync("532B84E5030001", "TOR-001");
        Assert.Equal(ResultadoEscaneo.ProductoNoEncontrado, resultado);
        Assert.Contains("no es un código NFC válido", Assert.Single(c.Candado.Escaneos).Rechazo);
    }

    [Fact]
    public async Task Lectura_doble_de_la_misma_pieza_no_se_duplica()
    {
        var c = Crear();
        await c.Servicio.RegistrarAsync("532B84E5030001", "A1B2C3");
        var (resultado, _) = await c.Servicio.RegistrarAsync("532B84E5030001", "A1B2C3");
        Assert.Equal(ResultadoEscaneo.PiezaRepetida, resultado);
        Assert.Single(c.Candado.Escaneos);
    }

    [Fact]
    public async Task Uid_que_no_es_hexadecimal_es_invalido()
    {
        var c = Crear();
        Assert.Equal(ResultadoEscaneo.CodigoInvalido, (await c.Servicio.RegistrarAsync("hola", "TOR-001")).Resultado);
        Assert.Empty(c.Candado.Escaneos);
    }

    [Fact]
    public async Task Sin_modo_inteligente_activo_no_se_registra()
    {
        var c = Crear(conSesion: false);
        Assert.Equal(ResultadoEscaneo.SinSesion, (await c.Servicio.RegistrarAsync("532B84E5030001", "A1B2C3")).Resultado);
        Assert.Empty(c.Candado.Escaneos);
    }
}

public class ServicioAccesoIotTests
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];
    private static readonly Guid Ana = Guid.NewGuid();

    private sealed class ReconocimientoFalso : IReconocimientoFacial
    {
        public List<PrediccionRostro> Predicciones { get; } = [];
        public Exception? Error { get; set; }
        public bool Configurado => true;
        public Task<string> SubirImagenAsync(string etiqueta, byte[] imagen, CancellationToken ct = default) => throw new NotSupportedException();
        public Task EliminarImagenesAsync(IReadOnlyCollection<string> imagenIds, CancellationToken ct = default) => throw new NotSupportedException();
        public Task EliminarEtiquetaAsync(string etiqueta, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<string, int>> ContarImagenesPorEtiquetaAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string> EntrenarYPublicarAsync(CancellationToken ct = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<PrediccionRostro>> ClasificarAsync(byte[] imagen, CancellationToken ct = default) =>
            Error is not null ? Task.FromException<IReadOnlyList<PrediccionRostro>>(Error) : Task.FromResult<IReadOnlyList<PrediccionRostro>>(Predicciones);
    }

    private sealed class BitacoraEnMemoria : IAccesosIotRepositorio, IUnidadDeTrabajo
    {
        public List<AccesoIot> Guardados { get; } = [];
        private readonly List<AccesoIot> _pendientes = [];
        public void Agregar(AccesoIot acceso) => _pendientes.Add(acceso);
        public Task<Pagina<AccesoIotDto>> ListarAsync(FiltroAccesosIot filtro, CancellationToken ct = default) => throw new NotSupportedException();

        public Task GuardarAsync(CancellationToken ct = default)
        {
            Guardados.AddRange(_pendientes);
            _pendientes.Clear();
            return Task.CompletedTask;
        }
    }

    private static (ServicioAccesoIot Servicio, ReconocimientoFalso Reconocimiento, BitacoraEnMemoria Bitacora) Crear(bool activo = true)
    {
        var usuarios = new GestorUsuariosEnMemoria();
        usuarios.Usuarios.Add(new UsuarioDto(Ana, "Ana López", "ana@x.mx", Roles.AgenteVentas, activo, "04A21B7C"));
        var reconocimiento = new ReconocimientoFalso();
        var bitacora = new BitacoraEnMemoria();
        var servicio = new ServicioAccesoIot(usuarios, reconocimiento, bitacora, bitacora, new RelojFijo(), new OpcionesIot { UmbralConfianza = 0.8 });
        return (servicio, reconocimiento, bitacora);
    }

    [Fact]
    public async Task Tarjeta_y_rostro_del_mismo_usuario_abren()
    {
        var (servicio, cv, bitacora) = Crear();
        cv.Predicciones.Add(new PrediccionRostro(Ana.ToString(), 0.97));

        var respuesta = await servicio.EvaluarAsync("04:a2:1b:7c", Jpeg);

        Assert.True(respuesta.Permitido);
        Assert.Equal(Ana, respuesta.UsuarioId);
        Assert.Equal("Ana López", respuesta.Nombre);
        var registro = Assert.Single(bitacora.Guardados);
        Assert.True(registro.Permitido);
        Assert.Equal("04A21B7C", registro.Nfc);
    }

    [Fact]
    public async Task Tarjeta_no_registrada_se_niega_y_queda_en_bitacora()
    {
        var (servicio, _, bitacora) = Crear();

        var respuesta = await servicio.EvaluarAsync("DEADBEEF", Jpeg);

        Assert.False(respuesta.Permitido);
        Assert.Equal("Tarjeta no registrada", respuesta.Motivo);
        Assert.Null(Assert.Single(bitacora.Guardados).UsuarioId);
    }

    [Fact]
    public async Task Usuario_desactivado_no_entra()
    {
        var (servicio, cv, _) = Crear(activo: false);
        cv.Predicciones.Add(new PrediccionRostro(Ana.ToString(), 0.99));
        Assert.False((await servicio.EvaluarAsync("04A21B7C", Jpeg)).Permitido);
    }

    [Fact]
    public async Task Imagen_que_no_es_jpeg_se_niega_sin_llamar_a_custom_vision()
    {
        var (servicio, cv, _) = Crear();
        cv.Error = new InvalidOperationException("no debería llamarse");

        var respuesta = await servicio.EvaluarAsync("04A21B7C", [0x89, 0x50, 0x4E, 0x47]);

        Assert.False(respuesta.Permitido);
        Assert.StartsWith("Imagen inválida", respuesta.Motivo);
    }

    [Fact]
    public async Task Imagen_rechazada_por_custom_vision_se_responde_negada()
    {
        var (servicio, cv, bitacora) = Crear();
        cv.Error = new ImagenInvalidaException("muy pequeña");

        var respuesta = await servicio.EvaluarAsync("04A21B7C", Jpeg);

        Assert.False(respuesta.Permitido);
        Assert.Single(bitacora.Guardados);
    }

    [Fact]
    public async Task Falla_transitoria_se_propaga_para_reintentar_y_no_se_registra()
    {
        var (servicio, cv, bitacora) = Crear();
        cv.Error = new HttpRequestException("503");

        await Assert.ThrowsAsync<HttpRequestException>(() => servicio.EvaluarAsync("04A21B7C", Jpeg));
        Assert.Empty(bitacora.Guardados);
    }
}
