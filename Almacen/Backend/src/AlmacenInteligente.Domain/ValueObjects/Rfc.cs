using System.Globalization;
using System.Text.RegularExpressions;
using AlmacenInteligente.Domain.Common;

namespace AlmacenInteligente.Domain.ValueObjects;

/// <summary>RFC mexicano: 12 caracteres persona moral, 13 persona física.</summary>
public sealed partial record Rfc
{
    public string Valor { get; }

    public bool EsPersonaFisica => Valor.Length == 13;

    private Rfc(string valor) => Valor = valor;

    public static Rfc Crear(string? valor)
    {
        var limpio = (valor ?? string.Empty).Trim().ToUpperInvariant().Replace(" ", "").Replace("-", "");
        var match = Formato().Match(limpio);
        if (!match.Success || !FechaValida(match.Groups["fecha"].Value))
            throw new DomainException("El RFC no es válido. Debe tener 12 caracteres (persona moral) o 13 (persona física).");
        return new Rfc(limpio);
    }

    private static bool FechaValida(string yymmdd) =>
        DateTime.TryParseExact(yymmdd, "yyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    [GeneratedRegex("^[A-ZÑ&]{3,4}(?<fecha>[0-9]{6})[A-Z0-9]{3}$")]
    private static partial Regex Formato();

    public override string ToString() => Valor;
}
