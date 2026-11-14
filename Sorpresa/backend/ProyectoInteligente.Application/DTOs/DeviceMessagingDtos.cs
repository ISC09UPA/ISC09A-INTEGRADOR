namespace LockerSmart.Application.DTOs;

/// <summary>Contrato JSON que la Raspberry Pi publica en la cola de eventos.</summary>
public sealed record DeviceEventMessage(
    string EventId,
    string LockerCodigo,
    string Tipo,
    DateTime? TimestampUtc = null,
    double? PesoKg = null,
    bool? PuertaCerrada = null,
    bool? PresenciaDetectada = null,
    int? RentaId = null,
    string? Detalle = null);

/// <summary>Comando que el backend publica para que Raspberry Pi lo ejecute.</summary>
public sealed record DeviceCommandMessage(
    string CommandId,
    string Accion,
    string LockerCodigo,
    int? RentaId,
    int UsuarioId,
    DateTime CreadoUtc,
    string? Motivo = null);

public sealed record EnviarComandoRequest(string Accion, string? Motivo);
