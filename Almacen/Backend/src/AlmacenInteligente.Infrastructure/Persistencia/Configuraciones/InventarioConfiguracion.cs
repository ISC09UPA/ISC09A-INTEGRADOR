using AlmacenInteligente.Domain.Almacenes;
using AlmacenInteligente.Domain.Finanzas;
using AlmacenInteligente.Domain.Inventario;
using AlmacenInteligente.Domain.Productos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlmacenInteligente.Infrastructure.Persistencia.Configuraciones;

internal sealed class ExistenciaConfiguracion : IEntityTypeConfiguration<Existencia>
{
    public void Configure(EntityTypeBuilder<Existencia> b)
    {
        b.ToTable("existencias");
        b.HasKey(e => e.Id);
        b.HasIndex(e => new { e.AlmacenId, e.ProductoId }).IsUnique();
        b.HasIndex(e => e.ProductoId);
        b.HasOne<Almacen>().WithMany().HasForeignKey(e => e.AlmacenId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Producto>().WithMany().HasForeignKey(e => e.ProductoId).OnDelete(DeleteBehavior.Restrict);
        b.Property(e => e.Cantidad).HasColumnType(Conversiones.TipoCantidad);
        b.Property(e => e.StockMinimo).HasColumnType(Conversiones.TipoCantidad);
        b.Property(e => e.CostoPromedio).HasColumnType(Conversiones.TipoCosto);
        b.Ignore(e => e.EstaBajoMinimo);
        b.Ignore(e => e.ValorInventario);
        b.ToTable(t => t.HasCheckConstraint("ck_existencias_cantidad_no_negativa", "cantidad >= 0"));

        // Control optimista: dos ventas que tocan la misma existencia chocan en vez de dejarla negativa.
        b.Property<uint>("Version").IsRowVersion(); // Npgsql lo mapea a la columna de sistema xmin
    }
}

internal sealed class MovimientoInventarioConfiguracion : IEntityTypeConfiguration<MovimientoInventario>
{
    public void Configure(EntityTypeBuilder<MovimientoInventario> b)
    {
        b.ToTable("movimientos_inventario");
        b.HasKey(m => m.Id);
        b.HasIndex(m => new { m.ProductoId, m.AlmacenId });
        b.HasOne<Almacen>().WithMany().HasForeignKey(m => m.AlmacenId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Producto>().WithMany().HasForeignKey(m => m.ProductoId).OnDelete(DeleteBehavior.Restrict);
        b.Property(m => m.Tipo).HasConversion<string>().HasMaxLength(30);
        b.Property(m => m.Cantidad).HasColumnType(Conversiones.TipoCantidad);
        b.Property(m => m.SaldoCantidad).HasColumnType(Conversiones.TipoCantidad);
        b.Property(m => m.CostoUnitario).HasColumnType(Conversiones.TipoCosto);
        b.Property(m => m.CostoPromedio).HasColumnType(Conversiones.TipoCosto);
        b.ComplexProperty(m => m.Documento, Conversiones.Documento);
        b.ComplexProperty(m => m.Registro, Conversiones.Auditoria);
    }
}

internal sealed class MovimientoFinancieroConfiguracion : IEntityTypeConfiguration<MovimientoFinanciero>
{
    public void Configure(EntityTypeBuilder<MovimientoFinanciero> b)
    {
        b.ToTable("movimientos_financieros");
        b.HasKey(m => m.Id);
        b.Property(m => m.Naturaleza).HasConversion<string>().HasMaxLength(20);
        b.ComplexProperty(m => m.Documento, Conversiones.Documento);
        b.ComplexProperty(m => m.Importe, Conversiones.Importe);
        b.ComplexProperty(m => m.Registro, Conversiones.Auditoria);
    }
}
