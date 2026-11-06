using LockerSmart.Domain.Enums;

namespace LockerSmart.Domain.Entities;

public class Locker
{
    public int Id { get; set; }
    public string Codigo { get; set; } = "";
    public LockerEstado Estado { get; set; } = LockerEstado.Disponible;
    public double PesoMaximoKg { get; set; } = 10;
    public double PesoActualKg { get; set; }
    public bool PuertaCerrada { get; set; } = true;
}
