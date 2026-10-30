using AirportParking.Domain.Common;

namespace AirportParking.Domain.Entities;

public class Vehicle : BaseEntity
{
    public string CustomerId { get; set; } = string.Empty;
    public string PlacaNormalizada { get; set; } = string.Empty;
    public string? Alias { get; set; }
    public string? Marca { get; set; }
    public string? Modelo { get; set; }
    public string? Color { get; set; }
    public bool EsPredeterminado { get; set; }
    public bool IsActive { get; set; } = true;
    
    public Customer? Customer { get; set; }
}
