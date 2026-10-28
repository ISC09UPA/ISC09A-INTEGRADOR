using System.Net.Mail;
using AlmacenInteligente.Domain.Common;

namespace AlmacenInteligente.Domain.ValueObjects;

public sealed record Correo
{
    public string Valor { get; }

    private Correo(string valor) => Valor = valor;

    public static Correo Crear(string? valor)
    {
        var limpio = (valor ?? string.Empty).Trim();
        if (limpio.Length is 0 or > 254
            || !MailAddress.TryCreate(limpio, out var direccion)
            || direccion.Address != limpio)
            throw new DomainException("El correo electrónico no es válido.");
        return new Correo(limpio.ToLowerInvariant());
    }

    public override string ToString() => Valor;
}
