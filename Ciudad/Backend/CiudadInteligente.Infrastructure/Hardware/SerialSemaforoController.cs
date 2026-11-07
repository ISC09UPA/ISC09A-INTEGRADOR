using System.IO.Ports;
using CiudadInteligente.Application.Exceptions;
using Microsoft.Extensions.Configuration;

namespace CiudadInteligente.Infrastructure.Hardware;

/// <summary>
/// El ESP32 esta conectado por USB a la misma maquina donde corre el backend
/// (produccion: backend en la Raspberry Pi). Se usa con
/// "Esp32:Transporte": "Serial" y "Esp32:Puerto" / "Esp32:BaudRate".
/// En Windows el puerto es tipo COM3 (Administrador de dispositivos >
/// Puertos COM y LPT); en Linux/Raspberry /dev/ttyUSB0 o /dev/ttyACM0.
/// </summary>
public sealed class SerialSemaforoController : SemaforoControllerBase, IDisposable
{
    private readonly SerialPort _puerto;
    private readonly object _lockPuerto = new();
    private volatile bool _abierto;

    public SerialSemaforoController(IConfiguration config)
    {
        var nombrePuerto = config["Esp32:Puerto"] ?? "COM3";
        var baudRate = int.TryParse(config["Esp32:BaudRate"], out var b) ? b : 115200;

        _puerto = new SerialPort(nombrePuerto, baudRate)
        {
            NewLine = "\n",
            ReadTimeout = 2000,
            WriteTimeout = 2000
        };
        _puerto.DataReceived += AlRecibirDatos;

        // Si el ESP32 no esta conectado al arrancar, el backend igual levanta
        // (la app muestra "ESP32 desconectado") y se reintenta al mandar el
        // siguiente comando.
        lock (_lockPuerto) IntentarAbrir();
    }

    protected override bool EstaConectado => _abierto;

    protected override Task EnviarLineaAsync(string comando)
    {
        lock (_lockPuerto)
        {
            if (!_puerto.IsOpen && !IntentarAbrir())
            {
                throw new HardwareNoDisponibleException(
                    $"No se pudo abrir el puerto serial '{_puerto.PortName}'. Revisa que el ESP32 este " +
                    "conectado y que Esp32:Puerto en appsettings.json sea el correcto.");
            }

            try
            {
                _puerto.WriteLine(comando);
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException)
            {
                _abierto = false;
                throw new HardwareNoDisponibleException("Se perdio la comunicacion serial con el ESP32.", ex);
            }
        }

        return Task.CompletedTask;
    }

    // con _lockPuerto tomado
    private bool IntentarAbrir()
    {
        try
        {
            if (!_puerto.IsOpen) _puerto.Open();
            _abierto = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                       or ArgumentException or InvalidOperationException)
        {
            _abierto = false;
        }

        return _abierto;
    }

    private void AlRecibirDatos(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            ProcesarTextoRecibido(_puerto.ReadExisting());
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException)
        {
            // el puerto se cerro mientras leiamos; el siguiente envio lo reabre
            _abierto = false;
        }
    }

    public void Dispose()
    {
        _puerto.DataReceived -= AlRecibirDatos;
        lock (_lockPuerto)
        {
            if (_puerto.IsOpen) _puerto.Close();
            _abierto = false;
        }
        _puerto.Dispose();
    }
}
