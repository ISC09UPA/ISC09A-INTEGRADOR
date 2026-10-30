using System.ComponentModel.DataAnnotations;

namespace Telemetria.Models;

public class LecturaSensor
{
    public int Id { get; set; }

    /// <summary>Identificador del sensor físico (ej. "DHT22-01").</summary>
    [Required, MaxLength(50)]
    public string SensorId { get; set; } = string.Empty;

    /// <summary>Tipo de medición: temperatura, humedad_aire, humedad_suelo, luz...</summary>
    [Required, MaxLength(30)]
    public string Tipo { get; set; } = string.Empty;

    public double Valor { get; set; }

    /// <summary>Unidad de la medición (°C, %, lux...).</summary>
    [Required, MaxLength(10)]
    public string Unidad { get; set; } = string.Empty;

    /// <summary>Invernadero al que pertenece el sensor (opcional).</summary>
    public int? InvernaderoId { get; set; }

    /// <summary>Fecha y hora de la lectura en UTC.</summary>
    public DateTime FechaHora { get; set; } = DateTime.UtcNow;
}