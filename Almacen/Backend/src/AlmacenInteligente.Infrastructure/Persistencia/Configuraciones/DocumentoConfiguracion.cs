using AlmacenInteligente.Domain.Almacenes;
using AlmacenInteligente.Domain.Documentos;
using AlmacenInteligente.Domain.Inventario;
using AlmacenInteligente.Domain.Productos;
using AlmacenInteligente.Domain.Recepciones;
using AlmacenInteligente.Domain.Terceros;
using AlmacenInteligente.Domain.Ventas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlmacenInteligente.Infrastructure.Persistencia.Configuraciones;

/// <summary>Recepciones, ventas y traspasos: cada uno en su tabla, con su folio consecutivo y sus líneas.</summary>
internal static class DocumentoConfiguracion
{
    public static void Configurar<T>(EntityTypeBuilder<T> b, string tabla) where T : Documento
    {
        b.ToTable(tabla);
        b.HasKey(d => d.Id);
        b.Property(d => d.Folio).UseIdentityAlwaysColumn();
        b.HasIndex(d => d.Folio).IsUnique();
        b.Property(d => d.Estado).HasConversion<string>().HasMaxLength(20);
        b.Property(d => d.MotivoCancelacion).HasMaxLength(500);
        b.ComplexProperty(d => d.Creado, Conversiones.Auditoria);
        b.ComplexProperty(d => d.Confirmado, Conversiones.Auditoria);
        b.ComplexProperty(d => d.Cancelado, Conversiones.Auditoria);
    }
}

internal sealed class RecepcionConfiguracion : IEntityTypeConfiguration<Recepcion>
{
    public void Configure(EntityTypeBuilder<Recepcion> b)
    {
        DocumentoConfiguracion.Configurar(b, "recepciones");
        b.HasIndex(r => r.Fecha);
        b.HasOne<Proveedor>().WithMany().HasForeignKey(r => r.ProveedorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Almacen>().WithMany().HasForeignKey(r => r.AlmacenId).OnDelete(DeleteBehavior.Restrict);
        b.Ignore(r => r.Importe);
        b.HasMany(r => r.Lineas).WithOne().HasForeignKey("RecepcionId").OnDelete(DeleteBehavior.Cascade);
        b.Navigation(r => r.Lineas).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
    }
}

internal sealed class LineaRecepcionConfiguracion : IEntityTypeConfiguration<LineaRecepcion>
{
    public void Configure(EntityTypeBuilder<LineaRecepcion> b)
    {
        b.ToTable("recepciones_lineas");
        b.HasKey(l => l.Id);
        b.Property(l => l.Id).ValueGeneratedNever();
        b.HasOne<Producto>().WithMany().HasForeignKey(l => l.ProductoId).OnDelete(DeleteBehavior.Restrict);
        b.Property(l => l.Cantidad).HasColumnType(Conversiones.TipoCantidad);
        b.Property(l => l.CostoUnitario).HasColumnType(Conversiones.TipoCosto);
        b.Property(l => l.TasaIva).Tasa();
        b.ComplexProperty(l => l.Importe, Conversiones.Importe);
    }
}

internal sealed class VentaConfiguracion : IEntityTypeConfiguration<Venta>
{
    public void Configure(EntityTypeBuilder<Venta> b)
    {
        DocumentoConfiguracion.Configurar(b, "ventas");
        b.HasIndex(v => v.Fecha);
        b.HasOne<Cliente>().WithMany().HasForeignKey(v => v.ClienteId).OnDelete(DeleteBehavior.Restrict);
        b.Ignore(v => v.Importe);
        b.HasMany(v => v.Lineas).WithOne().HasForeignKey("VentaId").OnDelete(DeleteBehavior.Cascade);
        b.Navigation(v => v.Lineas).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
    }
}

internal sealed class LineaVentaConfiguracion : IEntityTypeConfiguration<LineaVenta>
{
    public void Configure(EntityTypeBuilder<LineaVenta> b)
    {
        b.ToTable("ventas_lineas");
        b.HasKey(l => l.Id);
        b.Property(l => l.Id).ValueGeneratedNever();
        b.HasOne<Producto>().WithMany().HasForeignKey(l => l.ProductoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Almacen>().WithMany().HasForeignKey(l => l.AlmacenId).OnDelete(DeleteBehavior.Restrict);
        b.Property(l => l.Cantidad).HasColumnType(Conversiones.TipoCantidad);
        b.Property(l => l.PrecioUnitario).HasColumnType(Conversiones.TipoCosto);
        b.Property(l => l.CostoUnitario).HasColumnType(Conversiones.TipoCosto);
        b.Property(l => l.TasaIva).Tasa();
        b.ComplexProperty(l => l.Importe, Conversiones.Importe);
    }
}

internal sealed class TraspasoConfiguracion : IEntityTypeConfiguration<Traspaso>
{
    public void Configure(EntityTypeBuilder<Traspaso> b)
    {
        DocumentoConfiguracion.Configurar(b, "traspasos");
        b.HasIndex(t => t.Fecha);
        b.HasOne<Almacen>().WithMany().HasForeignKey(t => t.AlmacenOrigenId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Almacen>().WithMany().HasForeignKey(t => t.AlmacenDestinoId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(t => t.Lineas).WithOne().HasForeignKey("TraspasoId").OnDelete(DeleteBehavior.Cascade);
        b.Navigation(t => t.Lineas).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
    }
}

internal sealed class LineaTraspasoConfiguracion : IEntityTypeConfiguration<LineaTraspaso>
{
    public void Configure(EntityTypeBuilder<LineaTraspaso> b)
    {
        b.ToTable("traspasos_lineas");
        b.HasKey(l => l.Id);
        b.Property(l => l.Id).ValueGeneratedNever();
        b.HasOne<Producto>().WithMany().HasForeignKey(l => l.ProductoId).OnDelete(DeleteBehavior.Restrict);
        b.Property(l => l.Cantidad).HasColumnType(Conversiones.TipoCantidad);
        b.Property(l => l.CostoUnitario).HasColumnType(Conversiones.TipoCosto);
    }
}
