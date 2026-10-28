namespace AlmacenInteligente.Domain.Common;

/// <summary>Quién hizo algo y cuándo. Los ids de usuario vienen de Identity.</summary>
public sealed record Auditoria(Guid UsuarioId, DateTimeOffset Fecha);
