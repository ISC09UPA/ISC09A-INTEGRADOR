using System.Text.RegularExpressions;
using AlmacenInteligente.Domain.Common;

namespace AlmacenInteligente.Domain.ValueObjects;

/// <summary>Dirección segmentada. En México el código postal es de 5 dígitos.</summary>
public sealed partial record Direccion
{
    public const string Mexico = "México";

    public string Calle { get; }
    public string NumeroExterior { get; }
    public string? NumeroInterior { get; }
    public string Colonia { get; }
    public string CodigoPostal { get; }
    public string Municipio { get; }
    public string Estado { get; }
    public string Pais { get; }

    public Direccion(
        string calle, string numeroExterior, string? numeroInterior, string colonia,
        string codigoPostal, string municipio, string estado, string? pais = Mexico)
    {
        Calle = Guard.Requerido(calle, "La calle", 150);
        NumeroExterior = Guard.Requerido(numeroExterior, "El número exterior", 20);
        NumeroInterior = Guard.Opcional(numeroInterior, "El número interior", 20);
        Colonia = Guard.Requerido(colonia, "La colonia", 120);
        CodigoPostal = Guard.Requerido(codigoPostal, "El código postal", 10);
        Municipio = Guard.Requerido(municipio, "El municipio o alcaldía", 120);
        Estado = Guard.Requerido(estado, "El estado", 80);
        Pais = Guard.Opcional(pais, "El país", 80) ?? Mexico;

        if (Pais == Mexico && !CpMexico().IsMatch(CodigoPostal))
            throw new DomainException("El código postal debe tener 5 dígitos.");
    }

    [GeneratedRegex("^[0-9]{5}$")]
    private static partial Regex CpMexico();
}
