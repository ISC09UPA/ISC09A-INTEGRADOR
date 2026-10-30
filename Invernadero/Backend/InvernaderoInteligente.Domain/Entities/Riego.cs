using System.ComponentModel.DataAnnotations;

namespace Telemetria.Models;

public class Riego
{
    public int Id { get; set; }

    /// <summary>Invernadero donde se regó (opcional).</summary>
    public int? InvernaderoId { get; set; }

    /// <summary>Zona o válvula regada (ej. "Zona A").</summary>
    [MaxLength(50)]
    public string? Zona { get; set; }

    /// <summary>Fecha y hora de inicio del riego en UTC.</summary>
    public DateTime FechaHoraInicio { get; set; } = DateTime.UtcNow;

    /// <summary>Duración del riego en segundos.</summary>
    public int DuracionSegundos { get; set; }

    /// <summary>"automatico" (por umbral) o "manual".</summary>
    [Required, MaxLength(15)]
    public string Origen { get; set; } = "manual";

    /// <summary>Humedad de suelo que disparó el riego (solo automático).</summary>
    public double? HumedadDetectada { get; set; }

    /// <summary>Umbral configurado que se cruzó (solo automático).</summary>
    public double? Umbral { get; set; }

    /// <summary>Lectura de sensor que originó el riego (opcional).</summary>
    public int? LecturaSensorId { get; set; }
}