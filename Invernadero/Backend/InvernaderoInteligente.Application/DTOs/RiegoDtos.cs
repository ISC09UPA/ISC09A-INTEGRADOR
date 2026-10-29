using System.ComponentModel.DataAnnotations;

namespace Telemetria.DTOs;

public class CrearRiegoDto
{
    public int? InvernaderoId { get; set; }

    [MaxLength(50)]
    public string? Zona { get; set; }

    /// <summary>Si no se envía, se usa la hora del servidor.</summary>
    public DateTime? FechaHoraInicio { get; set; }

    [Required, Range(1, 86400)]
    public int? DuracionSegundos { get; set; }

    /// <summary>"automatico" o "manual". Por defecto "manual".</summary>
    [RegularExpression("^(automatico|manual)$", ErrorMessage = "Origen debe ser 'automatico' o 'manual'.")]
    public string Origen { get; set; } = "manual";

    public double? HumedadDetectada { get; set; }
    public double? Umbral { get; set; }
    public int? LecturaSensorId { get; set; }
}

public class RiegoDto
{
    public int Id { get; set; }
    public int? InvernaderoId { get; set; }
    public string? Zona { get; set; }
    public DateTime FechaHoraInicio { get; set; }
    public int DuracionSegundos { get; set; }
    public string Origen { get; set; } = string.Empty;
    public double? HumedadDetectada { get; set; }
    public double? Umbral { get; set; }
    public int? LecturaSensorId { get; set; }
}

public class FiltroRiegosDto
{
    public int? InvernaderoId { get; set; }
    public string? Zona { get; set; }
    public string? Origen { get; set; }
    public DateTime? Desde { get; set; }
    public DateTime? Hasta { get; set; }

    [Range(1, 1000)]
    public int Limite { get; set; } = 100;
}