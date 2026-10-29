using AlmacenInteligente.Domain.Common;
using AlmacenInteligente.Domain.Inventario;
using AlmacenInteligente.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlmacenInteligente.Infrastructure.Persistencia.Configuraciones;

/// <summary>Mapeos repetidos de value objects.</summary>
internal static class Conversiones
{
    public const string TipoDinero = "numeric(18,2)";
    public const string TipoCantidad = "numeric(18,3)";
    public const string TipoCosto = "numeric(18,4)";

    public static void Importe<T>(ComplexPropertyBuilder<T> b) where T : notnull
    {
        b.Property("Subtotal").HasConversion<DineroConvertidor>().HasColumnType(TipoDinero);
        b.Property("Iva").HasConversion<DineroConvertidor>().HasColumnType(TipoDinero);
    }

    public static PropertyBuilder<TasaIva> Tasa(this PropertyBuilder<TasaIva> b) =>
        b.HasConversion(t => t.Valor, v => new TasaIva(v)).HasColumnType("numeric(5,4)");

    public static void Documento(ComplexPropertyBuilder<DocumentoRef> b)
    {
        b.Property(d => d.Tipo).HasConversion<string>().HasMaxLength(20);
        b.Property(d => d.Id);
    }

    public static void Auditoria(ComplexPropertyBuilder<Auditoria> b)
    {
        b.Property(a => a.UsuarioId);
        b.Property(a => a.Fecha);
    }

    private sealed class DineroConvertidor()
        : Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<Dinero, decimal>(d => d.Monto, v => new Dinero(v));
}
