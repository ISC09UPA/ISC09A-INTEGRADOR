using LockerSmart.Domain.Enums;

namespace LockerSmart.Domain.Entities;

public class Renta
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public int LockerId { get; set; }

    public DateTime Inicio { get; set; } = DateTime.UtcNow;
    public DateTime Fin { get; set; }
    public DateTime? FechaFinalizacion { get; set; }

    public double PesoDeclaradoKg { get; set; }
    public double? PesoInicialKg { get; set; }
    public double? PesoFinalKg { get; set; }

    public RentaEstado Estado { get; set; } = RentaEstado.Activa;
}