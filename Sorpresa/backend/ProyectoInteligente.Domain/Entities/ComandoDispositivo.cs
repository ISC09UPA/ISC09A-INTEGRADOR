using LockerSmart.Domain.Enums;
namespace LockerSmart.Domain.Entities;
/// <summary>Historial de comandos enviados a un dispositivo físico.</summary>
public class ComandoDispositivo
{
    public int Id { get; set; }
    public string CommandId { get; set; } = "";
    public string LockerCodigo { get; set; } = "";
    public int? RentaId { get; set; }
    public int UsuarioId { get; set; }
    public string Accion { get; set; } = "";
    public ComandoEstado Estado { get; set; } = ComandoEstado.Pendiente;
    public string? Motivo { get; set; }
    public string? RespuestaDispositivo { get; set; }
    public DateTime CreadoUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ActualizadoUtc { get; set; }
}
