using AlmacenInteligente.Domain.Terceros;
using AlmacenInteligente.Domain.ValueObjects;
using FluentValidation;

namespace AlmacenInteligente.Application.Terceros;

// Piezas compartidas por clientes y proveedores: tienen los mismos datos y las mismas reglas.

public sealed record DireccionDto(
    string Calle, string NumeroExterior, string? NumeroInterior, string Colonia,
    string CodigoPostal, string Municipio, string Estado, string? Pais = Direccion.Mexico)
{
    public Direccion ADominio() =>
        new(Calle, NumeroExterior, NumeroInterior, Colonia, CodigoPostal, Municipio, Estado, Pais);

    public static DireccionDto DeDominio(Direccion d) =>
        new(d.Calle, d.NumeroExterior, d.NumeroInterior, d.Colonia, d.CodigoPostal, d.Municipio, d.Estado, d.Pais);
}

/// <summary>Lo que se captura al dar de alta o editar un cliente o proveedor.</summary>
public sealed record DatosTercero(string Codigo, string Nombre, string Correo, string Rfc, DireccionDto Direccion);

public sealed record TerceroDto(
    Guid Id, string Codigo, string Nombre, string Correo, string Rfc, DireccionDto Direccion, bool Activo)
{
    public static TerceroDto DeDominio(Tercero t) =>
        new(t.Id, t.Codigo, t.Nombre, t.Correo.Valor, t.Rfc.Valor, DireccionDto.DeDominio(t.Direccion), t.Activo);

    public DatosTercero ADatos() => new(Codigo, Nombre, Correo, Rfc, Direccion);
}

public sealed record Pagina<T>(IReadOnlyList<T> Elementos, int Total, int NumeroPagina, int Tamano);

/// <summary>Validación de forma; las reglas de negocio (RFC, CP, correo) las aplica el dominio.</summary>
internal sealed class DatosTerceroValidador : AbstractValidator<DatosTercero>
{
    public DatosTerceroValidador()
    {
        RuleFor(x => x.Codigo).NotEmpty().WithMessage("El código es obligatorio.").MaximumLength(30);
        RuleFor(x => x.Nombre).NotEmpty().WithMessage("El nombre es obligatorio.").MaximumLength(200);
        RuleFor(x => x.Correo).NotEmpty().WithMessage("El correo es obligatorio.");
        RuleFor(x => x.Rfc).NotEmpty().WithMessage("El RFC es obligatorio.");
        RuleFor(x => x.Direccion).NotNull().WithMessage("La dirección es obligatoria.");
    }
}
