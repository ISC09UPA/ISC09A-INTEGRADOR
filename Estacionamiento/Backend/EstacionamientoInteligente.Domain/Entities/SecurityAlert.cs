using AirportParking.Domain.Common;

namespace AirportParking.Domain.Entities;

public class SecurityAlert : BaseEntity
{
    public string PlacaNormalizada { get; set; } = string.Empty;
    public string DelitoReportado { get; set; } = string.Empty;
    public string FuenteBoletin { get; set; } = "C5 / Fiscalía General";
    public double ConfianzaCoincidencia { get; set; } = 0.98;
    public string EstadoDictamen { get; set; } = "PENDING_REVIEW"; // PENDING_REVIEW, CONFIRMED, DISMISSED
    public string? DictaminadoPor { get; set; }
    public string? JustificacionDictamen { get; set; }
    public DateTime? FechaDictamen { get; set; }
}
