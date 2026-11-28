namespace AlmacenInteligente.Domain.Common;

internal static class Guard
{
    public static string Requerido(string? valor, string campo, int maximo = 200)
    {
        var limpio = valor?.Trim();
        if (string.IsNullOrEmpty(limpio))
            throw new DomainException($"{campo} es obligatorio.");
        if (limpio.Length > maximo)
            throw new DomainException($"{campo} no puede exceder {maximo} caracteres.");
        return limpio;
    }

    public static string? Opcional(string? valor, string campo, int maximo = 200)
    {
        var limpio = valor?.Trim();
        if (string.IsNullOrEmpty(limpio))
            return null;
        if (limpio.Length > maximo)
            throw new DomainException($"{campo} no puede exceder {maximo} caracteres.");
        return limpio;
    }

    public static decimal Positivo(decimal valor, string campo)
    {
        if (valor <= 0)
            throw new DomainException($"{campo} debe ser mayor que cero.");
        return valor;
    }

    public static decimal NoNegativo(decimal valor, string campo)
    {
        if (valor < 0)
            throw new DomainException($"{campo} no puede ser negativo.");
        return valor;
    }

    public static Guid Id(Guid valor, string campo)
    {
        if (valor == Guid.Empty)
            throw new DomainException($"{campo} es obligatorio.");
        return valor;
    }
}
