namespace CiudadInteligente.Application.DTOs;

/// <summary>Cuerpo de POST api/semaforo/modo.</summary>
/// <param name="Modo">"adaptativo", "fijo" o "manual".</param>
public record CambiarModoDto(string Modo);

/// <summary>Cuerpo de POST api/semaforo/secuencia (solo en modo manual).</summary>
public record SecuenciaManualDto(IReadOnlyList<ColorAccesoDto> Accesos);

/// <param name="Acceso">1 Norte, 2 Sur, 3 Este, 4 Oeste.</param>
/// <param name="Color">"verde", "amarillo" o "rojo".</param>
public record ColorAccesoDto(int Acceso, string Color);
