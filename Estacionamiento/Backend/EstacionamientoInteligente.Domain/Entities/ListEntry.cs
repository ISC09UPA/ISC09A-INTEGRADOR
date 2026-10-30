using AirportParking.Domain.Common;

namespace AirportParking.Domain.Entities;

public class ListEntry : BaseEntity
{
    public string PlacaNormalizada { get; set; } = string.Empty;
    public string TipoLista { get; set; } = "WHITE_LIST"; // WHITE_LIST, BLACK_LIST
    public string Justificacion { get; set; } = string.Empty;
    public DateTime? VigenciaHasta { get; set; }
    public bool IsActive { get; set; } = true;
}
