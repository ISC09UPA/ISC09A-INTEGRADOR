using AlmacenInteligente.Application.Abstracciones;
using AlmacenInteligente.Application.Iot;
using AlmacenInteligente.Application.Terceros;
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
        public List<ConsultaStockIot> Guardadas { get; } = [];
        private readonly List<ConsultaStockIot> _pendientes = [];

        public Task<SemaforoStockDto> SemaforoAsync(FiltroSemaforoStock filtro, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<(decimal Existencia, decimal Minimo)> TotalesAsync(Guid productoId, CancellationToken ct = default) =>
            Task.FromResult(Totales.GetValueOrDefault(productoId));
        public Task<Pagina<ConsultaStockIotDto>> ListarConsultasAsync(FiltroConsultasStockIot filtro, CancellationToken ct = default) => throw new NotSupportedException();
        public void AgregarConsulta(ConsultaStockIot consulta) => _pendientes.Add(consulta);

        public Task GuardarAsync(CancellationToken ct = default)
        {
            Guardadas.AddRange(_pendientes);
            _pendientes.Clear();
            return Task.CompletedTask;
        }
    }

    private static (ServicioConsultaStockIot Servicio, StockEnMemoria Stock, VentasEnMemoria Ventas, Producto Tornillo) Crear()
    {
        var reloj = new RelojFijo();
        var productos = new ProductosEnMemoria();
        var tornillo = Producto.CrearAprobado("TOR-001", "Tornillo", new Unidad("PZA", "Pieza", permiteFracciones: false), new Auditoria(Guid.NewGuid(), reloj.Ahora));
        tornillo.AsignarCodigoNfc("A1B2C3");
        productos.Agregar(tornillo);
        var stock = new StockEnMemoria();
        var ventas = new VentasEnMemoria();
        return (new ServicioConsultaStockIot(productos, ventas, stock, stock, reloj), stock, ventas, tornillo);
    }

    [Theory]
    [InlineData(20, 5, "verde")]
    [InlineData(4, 5, "amarillo")]
    [InlineData(0, 5, "rojo")]
    public async Task Responde_existencia_total_y_el_led_que_toca(decimal existencia, decimal minimo, string led)
    {
        var (servicio, stock, _, tornillo) = Crear();
        stock.Totales[tornillo.Id] = (existencia, minimo);

        var r = await servicio.ConsultarAsync("532B84E5030001", "a1:b2:c3");

        Assert.Equal(ServicioConsultaStockIot.ModoConsulta, r.Modo);
        Assert.True(r.Aceptado);
        Assert.Equal(led, r.Nivel);
        Assert.Equal(existencia, r.Existencia);
        Assert.Equal("TOR-001 · Tornillo", r.Producto);
        var bitacora = Assert.Single(stock.Guardadas);
        Assert.Equal("532B84E5030001", bitacora.Uid);
        Assert.Equal(tornillo.Id, bitacora.ProductoId);
    }

    [Fact]
    public async Task Familia_desconocida_no_prende_led_y_queda_en_bitacora()
    {
        var (servicio, stock, _, _) = Crear();

        var r = await servicio.ConsultarAsync("532B84E5030001", "FFFF99");

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

        var r = await servicio.ConsultarAsync("532B84E5030001", "A1B2C3");

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
