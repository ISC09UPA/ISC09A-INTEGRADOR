using AlmacenInteligente.Domain.Common;
using AlmacenInteligente.Domain.Inventario;
using AlmacenInteligente.Domain.ValueObjects;

namespace AlmacenInteligente.Domain.Finanzas;

public enum NaturalezaFinanciera
{
    Ingreso = 1,
    Egreso = 2,
}

/// <summary>
/// Registro automático de lo comprado (egreso por recepción) y lo vendido (ingreso por venta).
/// Un reverso por cancelación lleva los importes en negativo, así los reportes solo suman.
/// </summary>
public sealed class MovimientoFinanciero : Entity
{
    public NaturalezaFinanciera Naturaleza { get; private init; }
    public DocumentoRef Documento { get; private init; } = null!;
    public Guid TerceroId { get; private init; }
    public Importe Importe { get; private init; }
    public bool EsReverso { get; private init; }
    public Auditoria Registro { get; private init; } = null!;

    private MovimientoFinanciero() { }

    private MovimientoFinanciero(
        NaturalezaFinanciera naturaleza, DocumentoRef documento, Guid terceroId, Importe importe, bool esReverso, Auditoria registro)
    {
        Naturaleza = naturaleza;
        Documento = documento;
        TerceroId = terceroId;
        Importe = importe;
        EsReverso = esReverso;
        Registro = registro;
    }

    internal static MovimientoFinanciero Registrar(
        NaturalezaFinanciera naturaleza, DocumentoRef documento, Guid terceroId, Importe importe, Auditoria registro) =>
        new(naturaleza, documento, terceroId, importe, esReverso: false, registro);

    internal static MovimientoFinanciero Reverso(
        NaturalezaFinanciera naturaleza, DocumentoRef documento, Guid terceroId, Importe importe, Auditoria registro) =>
        new(naturaleza, documento, terceroId, -importe, esReverso: true, registro);
}
