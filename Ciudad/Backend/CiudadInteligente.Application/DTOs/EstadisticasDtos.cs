namespace CiudadInteligente.Application.DTOs;

/// <summary>Todo lo de la pantalla Estadisticas en una sola respuesta (GET api/estadisticas/resumen).</summary>
/// <param name="ZonaHoraria">Las horas de este resumen estan en esta zona (America/Mexico_City).</param>
/// <param name="Muestras">Conteos de la camara (o del simulador) que se analizaron.</param>
public record ResumenEstadisticasDto(
    DateTimeOffset GeneradoEn,
    string ZonaHoraria,
    int DiasAnalizados,
    int Muestras,
    TraficoActualDto Actual,
    IReadOnlyList<FlujoHoraDto> FlujoPorHora,
    IReadOnlyList<FlujoDiaDto> FlujoPorDia,
    IReadOnlyList<int> HorasPico,
    MejorHoraDto? MejorHora,
    MejorHoraDto? MejorHoraProxima,
    PrediccionDto Prediccion,
    ComparativoDto Comparativo);

/// <summary>Lo que ve la camara ahora mismo.</summary>
/// <param name="Nivel">bajo, medio o alto (promedio por acceso).</param>
/// <param name="PromedioNormal">Vehiculos que suele haber a esta hora (null si aun no hay historial).</param>
/// <param name="ComparadoConNormal">menos, normal o mas (null si aun no hay historial).</param>
public record TraficoActualDto(
    int Total,
    string Nivel,
    IReadOnlyList<AccesoTraficoDto> Accesos,
    double? PromedioNormal,
    string? ComparadoConNormal);

public record AccesoTraficoDto(int Numero, string Nombre, int Vehiculos, string Nivel);

/// <summary>Promedio de vehiculos en una hora del dia (0-23, hora local).</summary>
/// <param name="PorAcceso">Promedio por acceso: indice 0 = Norte.</param>
/// <param name="Nivel">null si no hubo muestras a esa hora.</param>
public record FlujoHoraDto(
    int Hora,
    double Promedio,
    IReadOnlyList<double> PorAcceso,
    int Maximo,
    int Muestras,
    string? Nivel);

/// <param name="Dia">0 = domingo ... 6 = sabado.</param>
public record FlujoDiaDto(int Dia, string Nombre, double Promedio, int Muestras);

/// <param name="EnHoras">Para la mejor hora proxima: dentro de cuantas horas es (1-6).</param>
public record MejorHoraDto(int Hora, double Promedio, string Nivel, int? EnHoras);

/// <summary>Cuanto trafico habra en 15, 30 y 60 minutos.</summary>
/// <param name="Metodo">Como se calculo, en una frase (para mostrarlo en la app).</param>
/// <param name="Aviso">Mensaje corto si se espera congestion; null si no.</param>
public record PrediccionDto(
    double ActualReciente,
    IReadOnlyList<PuntoPrediccionDto> Puntos,
    bool CongestionPrevista,
    string? Aviso,
    string Metodo);

/// <param name="Tendencia">sube, baja o estable (contra el trafico actual).</param>
public record PuntoPrediccionDto(int Minutos, string Hora, double TotalEstimado, string Nivel, string Tendencia);

/// <summary>Espera promedio por vehiculo con temporizador fijo vs. con el algoritmo adaptativo.</summary>
/// <param name="EsperaFijoSegundos">Tiempos de la maqueta (el ciclo dura ~20 s).</param>
/// <param name="MejoraPorcentaje">Positivo = el adaptativo espera menos.</param>
/// <param name="Ahora">Lo mismo, solo con el conteo de este momento.</param>
public record ComparativoDto(
    double EsperaFijoSegundos,
    double EsperaAdaptativoSegundos,
    double MejoraPorcentaje,
    double CicloFijoSegundos,
    double CicloAdaptativoSegundos,
    int Muestras,
    long VehiculosAnalizados,
    IReadOnlyList<ComparativoAccesoDto> PorAcceso,
    ComparativoAccesoDto Ahora,
    string Modelo);

/// <param name="Numero">0 en "Ahora" (todo el cruce).</param>
public record ComparativoAccesoDto(int Numero, string Nombre, double FijoSegundos, double AdaptativoSegundos);
