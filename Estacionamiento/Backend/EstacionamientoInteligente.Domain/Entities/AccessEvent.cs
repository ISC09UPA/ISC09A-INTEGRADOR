using AirportParking.Domain.Common;

namespace AirportParking.Domain.Entities;

public class AccessEvent : BaseEntity
{
    public string LaneId { get; set; } = string.Empty;
    public string LaneType { get; set; } = "ENTRY"; // ENTRY, EXIT
    public string PlacaNormalizada { get; set; } = string.Empty;
    public double ConfianzaLpr { get; set; } = 0.95;
    public string Decision { get; set; } = "ALLOW"; // ALLOW, DENIED, REVIEW_REQUIRED
    public string Motivo { get; set; } = string.Empty;
    public string? EvidenciaImageUrl { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
