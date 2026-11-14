namespace AlmacenInteligente.Domain.Common;

/// <summary>Una regla de negocio fue violada. El mensaje se muestra al usuario.</summary>
public class DomainException(string message) : Exception(message);
