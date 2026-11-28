using AlmacenInteligente.Application.Abstracciones;
using AlmacenInteligente.Application.Iot;
using AlmacenInteligente.Application.Seguridad;
using AlmacenInteligente.Application.Terceros;
using AlmacenInteligente.Application.Usuarios;
using AlmacenInteligente.Domain.Common;
using AlmacenInteligente.Domain.Inventario;
using AlmacenInteligente.Domain.Iot;
using AlmacenInteligente.Domain.Productos;

namespace AlmacenInteligente.Application.Tests;

public class ServicioConsultaStockIotTests
{
    private sealed class StockEnMemoria : IConsultasStock, IUnidadDeTrabajo
    {
        public Dictionary<Guid, (decimal, decimal)> Totales { get; } = [];
        public List<Guid?> AlmacenesPedidos { get; } = [];
        public List<ConsultaStockIot> Guardadas { get; } = [];
        private readonly List<ConsultaStockIot> _pendientes = [];

        public Task<SemaforoStockDto> SemaforoAsync(FiltroSemaforoStock filtro, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<(decimal Existencia, decimal Minimo)> TotalesAsync(Guid productoId, Guid? almacenId, CancellationToken ct = default)
        {
            AlmacenesPedidos.Add(almacenId);
            return Task.FromResult(Totales.GetValueOrDefault(productoId));
        }
        public Task<Pagina<ConsultaStockIotDto>> ListarConsultasAsync(FiltroConsultasStockIot filtro, CancellationToken ct = default) => throw new NotSupportedException();
        public void AgregarConsulta(ConsultaStockIot consulta) => _pendientes.Add(consulta);

        public Task GuardarAsync(CancellationToken ct = default)
        {
            Guardadas.AddRange(_pendientes);
            _pendientes.Clear();
            return Task.CompletedTask;
        }
    }

    private static readonly Guid Ana = Guid.NewGuid();
    private static readonly Guid AlmacenDeAna = Guid.NewGuid();
    private static readonly Guid SinAlmacen = Guid.NewGuid();

    private static (ServicioConsultaStockIot Servicio, StockEnMemoria Stock, VentasEnMemoria Ventas, Producto Tornillo) Crear()
    {
        var reloj = new RelojFijo();
        var productos = new ProductosEnMemoria();
        var tornillo = Producto.CrearAprobado("TOR-001", "Tornillo", new Unidad("PZA", "Pieza", permiteFracciones: false), new Auditoria(Guid.NewGuid(), reloj.Ahora));
        tornillo.AsignarCodigoNfc("A1B2C3");
        productos.Agregar(tornillo);
        var stock = new StockEnMemoria();
        var ventas = new VentasEnMemoria();
        var usuarios = new GestorUsuariosEnMemoria();
        usuarios.Usuarios.Add(new UsuarioDto(Ana, "Ana López", "ana@x.mx", Roles.AgenteVentas, true, AlmacenId: AlmacenDeAna, Almacen: "Central"));
        usuarios.Usuarios.Add(new UsuarioDto(SinAlmacen, "Beto", "beto@x.mx", Roles.AgenteVentas, true));
        var candado = new CandadoEnMemoria(TimeSpan.FromMinutes(2));
        return (new ServicioConsultaStockIot(productos, ventas, stock, usuarios, candado, stock, reloj), stock, ventas, tornillo);
    }

    [Theory]
    [InlineData(20, 5, "verde")]
    [InlineData(4, 5, "amarillo")]
    [InlineData(0, 5, "rojo")]
    public async Task Responde_existencia_total_y_el_led_que_toca(decimal existencia, decimal minimo, string led)
    {
        var (servicio, stock, _, tornillo) = Crear();
        stock.Totales[tornillo.Id] = (existencia, minimo);

        var r = await servicio.ConsultarAsync("532B84E5030001", "a1:b2:c3", Ana);

        Assert.Equal(ServicioConsultaStockIot.ModoConsulta, r.Modo);
        Assert.True(r.Aceptado);
        Assert.Equal(led, r.Nivel);
        Assert.Equal(existencia, r.Existencia);
        Assert.Equal("TOR-001 · Tornillo", r.Producto);
        Assert.Equal(AlmacenDeAna, Assert.Single(stock.AlmacenesPedidos));
        var bitacora = Assert.Single(stock.Guardadas);
        Assert.Equal(AlmacenDeAna, bitacora.AlmacenId);
        Assert.Equal("532B84E5030001", bitacora.Uid);
        Assert.Equal(tornillo.Id, bitacora.ProductoId);
    }

    [Fact]
    public async Task Sin_usuario_con_sesion_en_el_lector_no_responde_stock()
    {
        var (servicio, stock, _, tornillo) = Crear();
        stock.Totales[tornillo.Id] = (10, 2);

        var r = await servicio.ConsultarAsync("532B84E5030001", "A1B2C3", null);

        Assert.False(r.Aceptado);
        Assert.Null(r.Existencia);
        Assert.Equal("Inicia sesión en el lector", r.Motivo);
    }

    [Fact]
    public async Task Usuario_sin_almacen_no_puede_comprobar()
    {
        var (servicio, _, _, _) = Crear();
        var r = await servicio.ConsultarAsync("532B84E5030001", "A1B2C3", SinAlmacen);
        Assert.Equal("Usuario sin almacén", r.Motivo);
    }

    [Fact]
    public async Task Familia_desconocida_no_prende_led_y_queda_en_bitacora()
    {
        var (servicio, stock, _, _) = Crear();

        var r = await servicio.ConsultarAsync("532B84E5030001", "FFFF99", Ana);

        Assert.False(r.Aceptado);
        Assert.Null(r.Nivel);
        Assert.Equal("Familia no registrada", r.Motivo);
        Assert.Null(Assert.Single(stock.Guardadas).Nivel);
    }

    [Fact]
    public async Task Pieza_ya_vendida_lo_avisa_pero_muestra_el_stock()
    {
        var (servicio, stock, ventas, tornillo) = Crear();
        stock.Totales[tornillo.Id] = (10, 2);
        ventas.Vendidas.Add("532B84E5030001");

        var r = await servicio.ConsultarAsync("532B84E5030001", "A1B2C3", Ana);

        Assert.Equal("Esta pieza ya se vendió", r.Motivo);
        Assert.Equal("verde", r.Nivel);
    }

    [Fact]
    public async Task En_venta_dice_si_se_agrego_y_el_nivel_del_producto()
    {
        var (servicio, stock, _, tornillo) = Crear();
        stock.Totales[tornillo.Id] = (3, 5);

        var r = await servicio.RespuestaVentaAsync(ResultadoEscaneo.Registrado, tornillo.Id);

        Assert.Equal(ServicioConsultaStockIot.ModoVenta, r.Modo);
        Assert.True(r.Aceptado);
        Assert.Equal("Agregada a la venta", r.Motivo);
        Assert.Equal("verde", r.Nivel);
        Assert.Equal(3, r.Existencia);
        Assert.Empty(stock.Guardadas);
    }

    [Theory]
    [InlineData(ResultadoEscaneo.PiezaRepetida, "amarillo", "Pieza duplicada")]
    [InlineData(ResultadoEscaneo.PiezaYaVendida, "rojo", "Pieza ya vendida")]
    public async Task En_venta_el_led_dice_que_paso(ResultadoEscaneo resultado, string led, string motivo)
    {
        var (servicio, _, _, tornillo) = Crear();
        var r = await servicio.RespuestaVentaAsync(resultado, tornillo.Id);
        Assert.False(r.Aceptado);
        Assert.Equal(led, r.Nivel);
        Assert.Equal(motivo, r.Motivo);
    }
}
