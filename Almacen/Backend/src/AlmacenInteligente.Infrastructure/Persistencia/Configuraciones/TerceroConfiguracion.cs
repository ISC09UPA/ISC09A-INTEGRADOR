using AlmacenInteligente.Domain.Terceros;
using AlmacenInteligente.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlmacenInteligente.Infrastructure.Persistencia.Configuraciones;

/// <summary>
/// Clientes y proveedores en tablas separadas; el código es único dentro de cada tabla.
/// No se mapea la clase base: EF no aplica ComplexProperty declarado en la base de una jerarquía TPC.
/// </summary>
internal static class TerceroConfiguracion
{
    public static void Configurar<T>(EntityTypeBuilder<T> b, string tabla) where T : Tercero
    {
        b.ToTable(tabla);
        b.HasKey(t => t.Id);
        b.HasIndex(t => t.Codigo).IsUnique();
        b.Property(t => t.Codigo).HasMaxLength(30);
        b.Property(t => t.Nombre).HasMaxLength(200);
        b.Property(t => t.Correo).HasMaxLength(254).HasConversion(c => c.Valor, v => Correo.Crear(v));
        b.Property(t => t.Rfc).HasMaxLength(13).HasConversion(r => r.Valor, v => Rfc.Crear(v));

        b.ComplexProperty(t => t.Direccion, d =>
        {
            d.Property(x => x.Calle).HasMaxLength(150);
            d.Property(x => x.NumeroExterior).HasMaxLength(20);
            d.Property(x => x.NumeroInterior).HasMaxLength(20);
            d.Property(x => x.Colonia).HasMaxLength(120);
            d.Property(x => x.CodigoPostal).HasMaxLength(10);
            d.Property(x => x.Municipio).HasMaxLength(120);
            d.Property(x => x.Estado).HasMaxLength(80);
            d.Property(x => x.Pais).HasMaxLength(80);
        });
        b.ComplexProperty(t => t.Creado);
    }
}

internal sealed class ClienteConfiguracion : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> b) => TerceroConfiguracion.Configurar(b, "clientes");
}

internal sealed class ProveedorConfiguracion : IEntityTypeConfiguration<Proveedor>
{
    public void Configure(EntityTypeBuilder<Proveedor> b) => TerceroConfiguracion.Configurar(b, "proveedores");
}
