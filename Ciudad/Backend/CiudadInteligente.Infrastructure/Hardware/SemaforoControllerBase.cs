using System.Globalization;
using System.Text;
using CiudadInteligente.Application.Interfaces;
using CiudadInteligente.Domain.Entities;
using CiudadInteligente.Domain.Enums;
using CiudadInteligente.Domain.Models;

namespace CiudadInteligente.Infrastructure.Hardware;

/// <summary>
/// Todo lo que es igual sin importar por donde viaja el texto hacia el ESP32:
/// arma los comandos del protocolo, lleva el estado del cruce y lee las
/// lineas EST: que manda el firmware. Las clases hijas solo deciden el
/// medio de transporte ("Esp32:Transporte" en appsettings.json):
///   - Serial:      SerialSemaforoController, ESP32 por USB en esta maquina.
///   - RaspberryPi: RaspberryPiSemaforoController, TCP al puente
///                  Embebidos/RaspberryPi/puente_serial (misma red).
///   - IoTHub:      IoTHubSemaforoController, Azure IoT Hub hacia
///                  Embebidos/RaspberryPi/agente_iot (cualquier red, sin IPs).
///
/// Protocolo (ver Embebidos/ESP32/semaforo_v2/semaforo_v2.ino):
///   MODO:AUTO | MODO:MANUAL | SET:{1-4}:VERDE|AMARILLO|ROJO
///   AMBULANCIA:{1-4} | AMBULANCIA:FIN | TRAFICO:{v1},{v2},{v3},{v4}
///   (respuesta del ESP32) EST:{0-4}:{fase}:{ms}
/// </summary>
public abstract class SemaforoControllerBase : ISemaforoController, IEstadoCruceProvider
{
    private const int Accesos = AccesoReglas.Cantidad;

    /// <summary>Si en tanto tiempo no llega ningun EST:, ya no se proyecta el ciclo.</summary>
    private static readonly TimeSpan MaximaProyeccion = TimeSpan.FromMinutes(3);

    private readonly object _lockEstado = new();
    private readonly StringBuilder _bufferLectura = new();

    private ModoSemaforo _modo = ModoSemaforo.Auto;
    private bool _adaptativo = true;
    private int _accesoEnTurno;
    private string _fase = "DESCONOCIDA";
    private int _duracionFaseMs;
    private DateTimeOffset _faseIniciadaEn = DateTimeOffset.UtcNow;
    private readonly ColorSemaforo?[] _colores = new ColorSemaforo?[Accesos];
    private readonly int[] _vehiculos = new int[Accesos];
    /// <summary>Lo ultimo que se le mando al ESP32 con TRAFICO: (define sus tiempos de verde).</summary>
    private readonly int[] _conteoEnEsp32 = new int[Accesos];
    private int? _accesoAmbulancia;
    private DateTimeOffset _actualizadoEn = DateTimeOffset.UtcNow;

    /// <summary>
    /// Manda una linea de texto al ESP32 por el medio que corresponda.
    /// Si no hay comunicacion debe lanzar HardwareNoDisponibleException.
    /// Nunca debe llamar de regreso a esta clase (evita bloqueos entre locks).
    /// </summary>
    protected abstract Task EnviarLineaAsync(string comando);

    /// <summary>True si en este momento hay comunicacion con el ESP32. Sin locks.</summary>
    protected abstract bool EstaConectado { get; }

    // ------------------------------------------------------------------
    // ISemaforoController
    // ------------------------------------------------------------------

    public Task SetModoAutoAsync() => EnviarConEstadoAsync("MODO:AUTO", () =>
    {
        _modo = ModoSemaforo.Auto;
        _accesoAmbulancia = null;
        // el firmware reinicia el ciclo y manda EST:0:TODOS_ROJO enseguida
        IniciarFase(0, "TODOS_ROJO", TiemposFirmware.TodosRojoMs);
    });

    public Task SetModoManualAsync() => EnviarConEstadoAsync("MODO:MANUAL", () =>
    {
        _modo = ModoSemaforo.Manual;
        _accesoAmbulancia = null;
        IniciarFase(0, "MANUAL", 0);
        // el firmware apaga todos los LED al entrar a manual
        Array.Fill(_colores, null);
    });

    public Task SetColorAsync(int acceso, ColorSemaforo color) =>
        EnviarConEstadoAsync($"SET:{acceso}:{color.ToString().ToUpperInvariant()}",
            () => _colores[acceso - 1] = color);

    public Task ActivarAmbulanciaAsync(int acceso) => EnviarConEstadoAsync($"AMBULANCIA:{acceso}", () =>
    {
        _modo = ModoSemaforo.Ambulancia;
        _accesoAmbulancia = acceso;
        IniciarFase(acceso, "AMBULANCIA", 0);
        for (var i = 0; i < Accesos; i++)
        {
            _colores[i] = i == acceso - 1 ? ColorSemaforo.Verde : ColorSemaforo.Rojo;
        }
    });

    // el firmware regresa solo a MODO_AUTO, reinicia el ciclo y manda EST:
    public Task FinalizarAmbulanciaAsync() => EnviarConEstadoAsync("AMBULANCIA:FIN", () =>
    {
        _modo = ModoSemaforo.Auto;
        _accesoAmbulancia = null;
        IniciarFase(0, "TODOS_ROJO", TiemposFirmware.TodosRojoMs);
    });

    public async Task ActualizarTraficoAsync(ConteoTrafico conteo)
    {
        var valores = conteo.ComoArreglo();

        bool reenviar;
        lock (_lockEstado)
        {
            Array.Copy(valores, _vehiculos, Accesos);
            _actualizadoEn = DateTimeOffset.UtcNow;
            reenviar = _adaptativo;
        }

        if (reenviar)
        {
            await EnviarTraficoAsync(valores);
        }
    }

    public async Task SetAdaptativoAsync(bool adaptativo)
    {
        int[] actuales;
        lock (_lockEstado)
        {
            _adaptativo = adaptativo;
            actuales = (int[])_vehiculos.Clone();
        }

        await SetModoAutoAsync();

        // con todo en 0 el firmware usa el verde fijo (DUR_VERDE)
        await EnviarTraficoAsync(adaptativo ? actuales : new int[Accesos]);
    }

    private async Task EnviarTraficoAsync(int[] valores)
    {
        await EnviarLineaAsync($"TRAFICO:{string.Join(',', valores)}");
        lock (_lockEstado) Array.Copy(valores, _conteoEnEsp32, Accesos);
    }

    // ------------------------------------------------------------------
    // IEstadoCruceProvider
    // ------------------------------------------------------------------

    public EstadoCruce ObtenerEstado()
    {
        var conectado = EstaConectado;
        var ahora = DateTimeOffset.UtcNow;

        lock (_lockEstado)
        {
            var (acceso, fase, duracion, inicio) = ProyectarFase(ahora);

            var colores = _modo == ModoSemaforo.Auto && fase != "DESCONOCIDA"
                ? ColoresDelCiclo(acceso, fase)
                : (ColorSemaforo?[])_colores.Clone();

            return new EstadoCruce(
                _modo,
                _adaptativo,
                conectado,
                acceso,
                fase,
                duracion,
                inicio,
                colores,
                (int[])_vehiculos.Clone(),
                _accesoAmbulancia,
                _actualizadoEn);
        }
    }

    /// <summary>
    /// A partir de la ultima fase conocida, avanza el ciclo con los tiempos del
    /// firmware hasta "ahora". Con Serial/RaspberryPi casi no hace nada (llega
    /// cada EST:); con IoT Hub es lo que rellena el ciclo entre mensajes.
    /// Se llama con _lockEstado tomado.
    /// </summary>
    private (int Acceso, string Fase, int DuracionMs, DateTimeOffset Inicio) ProyectarFase(DateTimeOffset ahora)
    {
        var acceso = _accesoEnTurno;
        var fase = _fase;
        var duracion = _duracionFaseMs;
        var inicio = _faseIniciadaEn;

        if (_modo != ModoSemaforo.Auto || duracion <= 0 || ahora - inicio > MaximaProyeccion)
        {
            return (acceso, fase, duracion, inicio);
        }

        // tope de vueltas por seguridad (3 min / fase mas corta ~ 360)
        for (var i = 0; i < 1000 && (ahora - inicio).TotalMilliseconds >= duracion; i++)
        {
            inicio = inicio.AddMilliseconds(duracion);
            (acceso, fase, duracion) = SiguienteFase(acceso, fase);
            if (duracion <= 0) break;
        }

        return (acceso, fase, duracion, inicio);
    }

    // Misma secuencia que actualizarAuto() del firmware. Con _lockEstado tomado.
    private (int Acceso, string Fase, int DuracionMs) SiguienteFase(int acceso, string fase) => fase switch
    {
        "TODOS_ROJO" => (1, "VERDE", TiemposFirmware.DuracionVerde(_conteoEnEsp32[0])),
        "VERDE" => (acceso, "PARPADEO", TiemposFirmware.ParpadeoMs),
        "PARPADEO" => (acceso, "AMARILLO", TiemposFirmware.AmarilloMs),
        "AMARILLO" => (acceso, "ROJO_FIN", TiemposFirmware.RojoFinMs),
        "ROJO_FIN" when acceso >= Accesos => (0, "TODOS_ROJO", TiemposFirmware.TodosRojoMs),
        "ROJO_FIN" => (acceso + 1, "VERDE", TiemposFirmware.DuracionVerde(_conteoEnEsp32[acceso])),
        _ => (acceso, fase, 0)
    };

    private static ColorSemaforo?[] ColoresDelCiclo(int acceso, string fase)
    {
        ColorSemaforo colorEnTurno = fase switch
        {
            "VERDE" or "PARPADEO" => ColorSemaforo.Verde,
            "AMARILLO" => ColorSemaforo.Amarillo,
            _ => ColorSemaforo.Rojo
        };

        var colores = new ColorSemaforo?[Accesos];
        for (var i = 0; i < Accesos; i++)
        {
            colores[i] = i == acceso - 1 ? colorEnTurno : ColorSemaforo.Rojo;
        }
        return colores;
    }

    // ------------------------------------------------------------------
    // Lectura de lo que manda el ESP32
    // ------------------------------------------------------------------

    /// <summary>
    /// Las clases hijas llaman esto con cada pedazo de texto que llega del
    /// ESP32 (puede traer media linea o varias lineas juntas).
    /// </summary>
    protected void ProcesarTextoRecibido(string texto)
    {
        lock (_lockEstado)
        {
            _bufferLectura.Append(texto);
            while (true)
            {
                var contenido = _bufferLectura.ToString();
                var salto = contenido.IndexOf('\n');
                if (salto < 0) break;

                var linea = contenido[..salto].Trim('\r', ' ');
                _bufferLectura.Remove(0, salto + 1);
                ProcesarLinea(linea);
            }

            // por si llega basura sin saltos de linea, no crecer sin limite
            if (_bufferLectura.Length > 4096) _bufferLectura.Clear();
        }
    }

    // Se llama con _lockEstado tomado.
    private void ProcesarLinea(string linea)
    {
        if (!linea.StartsWith("EST:", StringComparison.Ordinal)) return;

        // EST:<acceso 0-4>:<fase>:<ms>
        var partes = linea.Split(':');
        if (partes.Length != 4) return;
        if (!int.TryParse(partes[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var acceso)) return;
        if (!int.TryParse(partes[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ms)) return;
        if (acceso < 0 || acceso > Accesos) return;

        // las lineas EST: solo las manda el ciclo automatico; si llega una
        // atrasada despues de pasar a manual o ambulancia, se ignora
        if (_modo != ModoSemaforo.Auto) return;

        IniciarFase(acceso, partes[2], ms);
        Array.Copy(ColoresDelCiclo(acceso, partes[2]), _colores, Accesos);
    }

    // Con _lockEstado tomado.
    private void IniciarFase(int acceso, string fase, int duracionMs)
    {
        _accesoEnTurno = acceso;
        _fase = fase;
        _duracionFaseMs = duracionMs;
        _faseIniciadaEn = DateTimeOffset.UtcNow;
        _actualizadoEn = _faseIniciadaEn;
    }

    // ------------------------------------------------------------------
    // Envio con actualizacion de estado
    // ------------------------------------------------------------------

    /// <summary>
    /// Actualiza el estado ANTES de mandar el comando (el ESP32 puede contestar
    /// con EST: casi al instante y esa linea debe encontrar ya el modo nuevo).
    /// Si el envio falla, el estado regresa a como estaba y se relanza el error.
    /// </summary>
    private async Task EnviarConEstadoAsync(string comando, Action cambio)
    {
        Respaldo respaldo;
        lock (_lockEstado)
        {
            respaldo = Respaldar();
            cambio();
            _actualizadoEn = DateTimeOffset.UtcNow;
        }

        try
        {
            await EnviarLineaAsync(comando);
        }
        catch
        {
            lock (_lockEstado) Restaurar(respaldo);
            throw;
        }
    }

    private sealed record Respaldo(
        ModoSemaforo Modo, int AccesoEnTurno, string Fase, int DuracionFaseMs,
        DateTimeOffset FaseIniciadaEn, ColorSemaforo?[] Colores, int? AccesoAmbulancia,
        DateTimeOffset ActualizadoEn);

    private Respaldo Respaldar() => new(
        _modo, _accesoEnTurno, _fase, _duracionFaseMs, _faseIniciadaEn,
        (ColorSemaforo?[])_colores.Clone(), _accesoAmbulancia, _actualizadoEn);

    private void Restaurar(Respaldo r)
    {
        _modo = r.Modo;
        _accesoEnTurno = r.AccesoEnTurno;
        _fase = r.Fase;
        _duracionFaseMs = r.DuracionFaseMs;
        _faseIniciadaEn = r.FaseIniciadaEn;
        Array.Copy(r.Colores, _colores, Accesos);
        _accesoAmbulancia = r.AccesoAmbulancia;
        _actualizadoEn = r.ActualizadoEn;
    }
}
