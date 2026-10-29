using System.ComponentModel.DataAnnotations;

namespace Telemetria.DTOs;

/// <summary>Datos que envía el sensor (o el dispositivo) en el POST.</summary>
public class CrearLecturaDto
{
    [Required, MaxLength(50)]
    public string SensorId { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    public string Tipo { get; set; } = string.Empty;

    [Required]
    public double? Valor { get; set; }

    [Required, MaxLength(10)]
    public string Unidad { get; set; } = string.Empty;

    public int? InvernaderoId { get; set; }

    /// <summary>Si no se envía, se usa la hora del servidor.</summary>
    public DateTime? FechaHora { get; set; }
}

/// <summary>Datos que regresa la API.</summary>
public class LecturaDto
{
    public int Id { get; set; }
    public string SensorId { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public double Valor { get; set; }
    public string Unidad { get; set; } = string.Empty;
    public int? InvernaderoId { get; set; }
    public DateTime FechaHora { get; set; }
}

/// <summary>Filtros opcionales del GET.</summary>
public class FiltroLecturasDto
{
    public string? SensorId { get; set; }
    public string? Tipo { get; set; }
    public int? InvernaderoId { get; set; }
    public DateTime? Desde { get; set; }
    public DateTime? Hasta { get; set; }

    /// <summary>Máximo de lecturas a devolver (1-1000, por defecto 100).</summary>
    [Range(1, 1000)]
    public int Limite { get; set; } = 100;
}