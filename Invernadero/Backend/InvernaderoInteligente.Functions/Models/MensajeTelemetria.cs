using InvernaderoInteligente.Application.DTOs;

namespace InvernaderoInteligente.Functions.Models;

/// <summary>
/// Valores válidos del campo raíz <c>tipo</c> (acordado con Juan en SCRUM-19).
/// Se acepta también "lecturas" y el nombre viejo <c>tipoMensaje</c> por compatibilidad.
/// </summary>
public static class TiposMensaje
{
    public const string Lectura = "lectura";
    public const string Riego = "riego";

    /// <summary>Normaliza el tipo recibido; devuelve null si no se reconoce.</summary>
    public static string? Normalizar(string? valor) => valor?.Trim().ToLowerInvariant() switch
    {
        "lectura" or "lecturas" => Lectura,
        "riego" or "riegos" => Riego,
        _ => null
    };
}

/// <summary>
/// Catálogo acordado de <c>tipo</c> para cada lectura (ver CONTRATOS.md).
/// Un tipo fuera del catálogo se guarda igual pero deja un warning en los logs.
/// </summary>
public static class TiposLectura
{
    public static readonly IReadOnlyDictionary<string, string> UnidadPorTipo = new Dictionary<string, string>
    {
        ["temperatura"] = "°C",
        ["humedad_aire"] = "%",
        ["humedad_suelo"] = "%",
        ["luz"] = "lux",
        ["nivel_deposito"] = "%",
        ["caudal"] = "L/min",
        ["flotador_vacio"] = "bool",
    };

    public static bool EsConocido(string? tipo)
        => tipo is not null && UnidadPorTipo.ContainsKey(tipo.Trim().ToLowerInvariant());
}

/// <summary>
/// Mensaje que la Raspberry manda a IoT Hub y que IoT Hub enruta al Service Bus.
/// Ver Ejemplos/ y CONTRATOS.md para el formato exacto.
/// </summary>
public class MensajeTelemetria
{
    /// <summary>Máximo de lecturas por mensaje (IoT Hub limita el mensaje a 256 KB).</summary>
    public const int MaxLecturasPorMensaje = 200;

    /// <summary>Campo oficial: "lectura" o "riego" (SCRUM-19).</summary>
    public string? Tipo { get; set; }

    /// <summary>Alias aceptado por compatibilidad; si viene <see cref="Tipo"/>, gana <see cref="Tipo"/>.</summary>
    public string? TipoMensaje { get; set; }

    /// <summary>Id del dispositivo en IoT Hub. Si no viene, se toma de la propiedad que agrega IoT Hub.</summary>
    public string? DispositivoId { get; set; }

    /// <summary>Se aplica a cada lectura o riego que no traiga su propio InvernaderoId.</summary>
    public int? InvernaderoId { get; set; }

    /// <summary>Lecturas del ciclo (cuando TipoMensaje = "lecturas").</summary>
    public List<CrearLecturaDto>? Lecturas { get; set; }

    /// <summary>Riego realizado (cuando TipoMensaje = "riego").</summary>
    public CrearRiegoDto? Riego { get; set; }
}
