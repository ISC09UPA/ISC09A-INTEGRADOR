using AlmacenInteligente.Domain.Common;
using AlmacenInteligente.Domain.ValueObjects;

namespace AlmacenInteligente.Domain.Terceros;

public sealed class Proveedor : Tercero
{
    private Proveedor() { }

    public Proveedor(string codigo, string nombre, Correo correo, Rfc rfc, Direccion direccion, Auditoria creado)
        : base(codigo, nombre, correo, rfc, direccion, creado) { }
}
