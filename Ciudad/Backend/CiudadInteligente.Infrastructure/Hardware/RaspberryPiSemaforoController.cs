using System.Net.Sockets;
using System.Text;
using CiudadInteligente.Application.Exceptions;
using Microsoft.Extensions.Configuration;

namespace CiudadInteligente.Infrastructure.Hardware;

/// <summary>
/// Fase de pruebas: el backend corre en la PC y el ESP32 esta conectado por
/// USB a la Raspberry Pi. Los comandos viajan por TCP al puente
/// Embebidos/RaspberryPi/puente_serial/puente_serial.py, que los escribe al
/// serial del ESP32 y regresa sus respuestas (OK:, ERR:, EST:).
///
/// Se usa con "Esp32:Transporte": "RaspberryPi" y
/// "Esp32:RaspberryPi:Host" / "Esp32:RaspberryPi:Puerto" en appsettings.json.
/// Si el puente se cae, se reconecta solo cada pocos segundos.
/// </summary>
public sealed class RaspberryPiSemaforoController : SemaforoControllerBase, IDisposable
{
    private static readonly TimeSpan TiempoConexion = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan EsperaReintento = TimeSpan.FromSeconds(3);

    private readonly string _host;
    private readonly int _puertoTcp;
    private readonly object _lockConexion = new();
    private readonly CancellationTokenSource _cts = new();

    private TcpClient? _cliente;
    private NetworkStream? _stream;
    private volatile bool _conectado;

    public RaspberryPiSemaforoController(IConfiguration config)
    {
        _host = config["Esp32:RaspberryPi:Host"] ?? "raspberrypi.local";
        _puertoTcp = int.TryParse(config["Esp32:RaspberryPi:Puerto"], out var p) ? p : 5090;

        // escucha continua de lo que manda el ESP32 (lineas EST: para la app)
        _ = Task.Run(() => BucleLecturaAsync(_cts.Token));
    }

    protected override bool EstaConectado => _conectado;

    protected override Task EnviarLineaAsync(string comando)
    {
        lock (_lockConexion)
        {
            if (!AsegurarConexion())
            {
                throw new HardwareNoDisponibleException(
                    $"No se pudo conectar con el puente de la Raspberry Pi en {_host}:{_puertoTcp}. " +
                    "Revisa que puente_serial.py este corriendo en la Raspberry, que esten en la misma red " +
                    "y que Esp32:RaspberryPi:Host en appsettings.json sea la IP correcta.");
            }

            try
            {
                var bytes = Encoding.ASCII.GetBytes(comando + "\n");
                _stream!.Write(bytes, 0, bytes.Length);
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or SocketException)
            {
                Cerrar();
                throw new HardwareNoDisponibleException("Se perdio la conexion con el puente de la Raspberry Pi.", ex);
            }
        }

        return Task.CompletedTask;
    }

    // con _lockConexion tomado
    private bool AsegurarConexion()
    {
        if (_cliente is { Connected: true } && _stream is not null) return true;

        Cerrar();
        var cliente = new TcpClient { NoDelay = true };
        try
        {
            if (!cliente.ConnectAsync(_host, _puertoTcp).Wait(TiempoConexion))
            {
                cliente.Dispose();
                return false;
            }

            _cliente = cliente;
            _stream = cliente.GetStream();
            _conectado = true;
            return true;
        }
        catch (Exception ex) when (ex is AggregateException or SocketException or IOException)
        {
            cliente.Dispose();
            return false;
        }
    }

    // con _lockConexion tomado
    private void Cerrar()
    {
        _conectado = false;
        _stream?.Dispose();
        _cliente?.Dispose();
        _stream = null;
        _cliente = null;
    }

    private async Task BucleLecturaAsync(CancellationToken ct)
    {
        var buffer = new byte[1024];
        while (!ct.IsCancellationRequested)
        {
            NetworkStream? stream;
            lock (_lockConexion)
            {
                AsegurarConexion();
                stream = _stream;
            }

            if (stream is null)
            {
                try { await Task.Delay(EsperaReintento, ct); }
                catch (OperationCanceledException) { break; }
                continue;
            }

            try
            {
                var leidos = await stream.ReadAsync(buffer, ct);
                if (leidos == 0)
                {
                    // el puente cerro la conexion
                    lock (_lockConexion) Cerrar();
                    continue;
                }

                ProcesarTextoRecibido(Encoding.ASCII.GetString(buffer, 0, leidos));
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or SocketException)
            {
                lock (_lockConexion) Cerrar();
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        lock (_lockConexion) Cerrar();
        _cts.Dispose();
    }
}
