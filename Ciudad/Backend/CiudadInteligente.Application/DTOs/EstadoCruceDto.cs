namespace CiudadInteligente.Application.DTOs;

/// <summary>Respuesta de GET api/semaforo/estado.</summary>
/// <param name="Modo">"auto", "manual" o "ambulancia".</param>
/// <param name="Adaptativo">En auto: true = verde segun densidad, false = temporizador fijo.</param>
/// <param name="AccesoEnTurno">0 = todos en rojo; 1-4 = acceso con el turno.</param>
/// <param name="Fase">TODOS_ROJO, VERDE, PARPADEO, AMARILLO, ROJO_FIN, MANUAL, AMBULANCIA o DESCONOCIDA.</param>
/// <param name="DuracionFaseMs">Cuanto dura la fase actual (0 si no aplica).</param>
/// <param name="MsTranscurridos">Cuanto lleva la fase actual al momento de responder.</param>
public record EstadoCruceDto(
    string Modo,
    bool Adaptativo,
    bool Esp32Conectado,
    int AccesoEnTurno,
    string Fase,
    int DuracionFaseMs,
    int MsTranscurridos,
    IReadOnlyList<AccesoEstadoDto> Accesos,
    int TotalVehiculos,
    int? AccesoAmbulancia,
    DateTimeOffset ActualizadoEn);

/// <param name="Color">"verde", "amarillo", "rojo" o null si esta apagado.</param>
/// <param name="Nivel">"bajo", "medio" o "alto" segun los vehiculos.</param>
public record AccesoEstadoDto(
    int Numero,
    string Nombre,
    string? Color,
    int Vehiculos,
    string Nivel);
