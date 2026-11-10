namespace CiudadInteligente.Application.Services;

/// <summary>
/// Zona horaria de las estadisticas ("Estadisticas:ZonaHoraria" en
/// appsettings.json). Las fechas se guardan en UTC; las horas pico, la mejor
/// hora, etc. se calculan en hora de Mexico aunque el backend corra en Azure (UTC).
/// </summary>
public sealed record OpcionesEstadisticas(TimeZoneInfo Zona);
