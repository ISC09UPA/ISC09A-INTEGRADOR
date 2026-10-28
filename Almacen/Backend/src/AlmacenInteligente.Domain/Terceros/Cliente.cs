using AlmacenInteligente.Domain.Common;
using AlmacenInteligente.Domain.ValueObjects;

namespace AlmacenInteligente.Domain.Terceros;

public sealed class Cliente : Tercero
{
    private Cliente() { }

    public Cliente(string codigo, string nombre, Correo correo, Rfc rfc, Direccion direccion, Auditoria creado)
        : base(codigo, nombre, correo, rfc, direccion, creado) { }
}
