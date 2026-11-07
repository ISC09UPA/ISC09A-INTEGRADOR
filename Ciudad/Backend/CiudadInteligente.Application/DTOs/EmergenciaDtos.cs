namespace CiudadInteligente.Application.DTOs;

/// <summary>Cuerpo de POST api/emergencias.</summary>
/// <param name="Unidad">Ej. "AMB-042 Cruz Roja".</param>
/// <param name="Acceso">1 Norte, 2 Sur, 3 Este, 4 Oeste: por donde llega la unidad.</param>
/// <param name="Tipo">"medica", "incendio" o "seguridad".</param>
/// <param name="EtaMinutos">Tiempo estimado de llegada (0 a 120).</param>
public record SolicitudEmergenciaDto(string Unidad, int Acceso, string Tipo, int EtaMinutos);

/// <summary>Una emergencia como la ve la app.</summary>
/// <param name="Registrada">False si el corredor se activo pero no se pudo guardar en la base de datos.</param>
public record EmergenciaDto(
    int Id,
    string Unidad,
    int Acceso,
    string NombreAcceso,
    string Tipo,
    int EtaMinutos,
    DateTimeOffset SolicitadaEn,
    DateTimeOffset? FinalizadaEn,
    bool Activa,
    bool Registrada);
