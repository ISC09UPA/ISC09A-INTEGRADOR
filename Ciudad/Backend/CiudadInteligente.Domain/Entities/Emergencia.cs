using CiudadInteligente.Domain.Enums;

namespace CiudadInteligente.Domain.Entities;

/// <summary>
/// Un corredor de emergencia pedido desde la app: que unidad, por que acceso
/// llega, que tipo de emergencia y cuando empezo / termino.
/// Se guarda en la tabla Emergencias (SQLite).
/// </summary>
public class Emergencia
{
    public int Id { get; set; }

    /// <summary>Identificador de la unidad, p. ej. "AMB-042 Cruz Roja".</summary>
    public string Unidad { get; set; } = string.Empty;

    /// <summary>Acceso por el que llega la unidad (1 Norte, 2 Sur, 3 Este, 4 Oeste).</summary>
    public int Acceso { get; set; }

    public TipoEmergencia Tipo { get; set; }

    /// <summary>Tiempo estimado de llegada que reporto la unidad, en minutos.</summary>
    public int EtaMinutos { get; set; }

    public DateTimeOffset SolicitadaEn { get; set; }

    /// <summary>Null mientras el corredor sigue activo.</summary>
    public DateTimeOffset? FinalizadaEn { get; set; }

    public bool EstaActiva => FinalizadaEn is null;
}
