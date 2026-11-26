namespace CiudadInteligente.Application.DTOs;

/// <summary>
/// Lo que ve un usuario normal (sin cuenta) del cruce: GET api/publico/cruce.
/// A proposito NO incluye si el ESP32 esta conectado, el modo adaptativo/fijo
/// ni el acceso de la ambulancia: eso es solo para operadores.
/// </summary>
/// <param name="Modo">"auto", "manual" o "ambulancia".</param>
/// <param name="AccesoEnTurno">0 = todos en rojo; 1-4 = acceso con el turno.</param>
/// <param name="Fase">TODOS_ROJO, VERDE, PARPADEO, AMARILLO, ROJO_FIN, MANUAL, AMBULANCIA o DESCONOCIDA.</param>
/// <param name="EmergenciaActiva">true = hay un corredor de emergencia en curso (se avisa, sin decir donde).</param>
public record EstadoPublicoDto(
    string Modo,
    int AccesoEnTurno,
    string Fase,
    int DuracionFaseMs,
    int MsTranscurridos,
    IReadOnlyList<AccesoEstadoDto> Accesos,
    int TotalVehiculos,
    bool EmergenciaActiva,
    DateTimeOffset ActualizadoEn)
{
    public static EstadoPublicoDto De(EstadoCruceDto estado) => new(
        estado.Modo,
        estado.AccesoEnTurno,
        estado.Fase,
        estado.DuracionFaseMs,
        estado.MsTranscurridos,
        estado.Accesos,
        estado.TotalVehiculos,
        estado.AccesoAmbulancia is not null,
        estado.ActualizadoEn);
}

/// <summary>
/// Trafico para un usuario normal: GET api/publico/trafico. Lo mismo que la
/// pantalla Estadisticas, sin el comparativo ni los conteos internos.
/// </summary>
public record TraficoPublicoDto(
    DateTimeOffset GeneradoEn,
    string ZonaHoraria,
    TraficoActualDto Actual,
    PrediccionDto Prediccion,
    IReadOnlyList<FlujoHoraDto> FlujoPorHora,
    IReadOnlyList<int> HorasPico,
    MejorHoraDto? MejorHora,
    MejorHoraDto? MejorHoraProxima)
{
    public static TraficoPublicoDto De(ResumenEstadisticasDto resumen) => new(
        resumen.GeneradoEn,
        resumen.ZonaHoraria,
        resumen.Actual,
        resumen.Prediccion,
        resumen.FlujoPorHora,
        resumen.HorasPico,
        resumen.MejorHora,
        resumen.MejorHoraProxima);
}
