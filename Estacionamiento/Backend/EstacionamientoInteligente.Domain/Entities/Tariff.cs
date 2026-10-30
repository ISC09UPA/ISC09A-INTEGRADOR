using AirportParking.Domain.Common;

namespace AirportParking.Domain.Entities;

public class Tariff : BaseEntity
{
    public int Version { get; set; } = 1;
    public int FraccionMinutos { get; set; } = 15;
    public decimal CostoFraccion { get; set; } = 20.00m; // $20.00 per 15 min ($80/h) or $11.25 per 15m ($45/h)
    public decimal TopeDiario { get; set; } = 350.00m;
    public int ToleranciaSalidaMinutos { get; set; } = 15;
    public DateTime VigenciaDesde { get; set; } = DateTime.UtcNow;
    public DateTime? VigenciaHasta { get; set; }
    public bool IsActive { get; set; } = true;
}
