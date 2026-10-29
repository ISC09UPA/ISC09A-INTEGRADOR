using AlmacenInteligente.Domain.Almacenes;
using AlmacenInteligente.Domain.Productos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlmacenInteligente.Infrastructure.Persistencia.Configuraciones;

internal sealed class UnidadConfiguracion : IEntityTypeConfiguration<Unidad>
{
    public void Configure(EntityTypeBuilder<Unidad> b)
    {
        b.ToTable("unidades");
        b.HasKey(u => u.Id);
        b.HasIndex(u => u.Clave).IsUnique();
        b.Property(u => u.Clave).HasMaxLength(10);
        b.Property(u => u.Nombre).HasMaxLength(60);
    }
}

internal sealed class ProductoConfiguracion : IEntityTypeConfiguration<Producto>
{
    public void Configure(EntityTypeBuilder<Producto> b)
    {
        b.ToTable("productos");
        b.HasKey(p => p.Id);
        b.HasIndex(p => p.Referencia).IsUnique();
        b.HasIndex(p => new { p.Estado, p.GerenteAprobadorId });
        b.Property(p => p.Referencia).HasMaxLength(50);
        b.Property(p => p.Descripcion).HasMaxLength(500);
        b.Property(p => p.ImagenRuta).HasMaxLength(400);
        b.Property(p => p.MotivoRechazo).HasMaxLength(500);
        b.Property(p => p.Estado).HasConversion<string>().HasMaxLength(20);
        b.HasOne<Unidad>().WithMany().HasForeignKey(p => p.UnidadId).OnDelete(DeleteBehavior.Restrict);
        b.ComplexProperty(p => p.Creado, Conversiones.Auditoria);
        b.ComplexProperty(p => p.Resolucion, Conversiones.Auditoria);
        b.Ignore(p => p.EstaDisponible);
    }
}

internal sealed class AlmacenConfiguracion : IEntityTypeConfiguration<Almacen>
{
    public void Configure(EntityTypeBuilder<Almacen> b)
    {
        b.ToTable("almacenes");
        b.HasKey(a => a.Id);
        b.HasIndex(a => a.Nombre).IsUnique();
        b.Property(a => a.Nombre).HasMaxLength(120);
        b.Property(a => a.Ubicacion).HasMaxLength(300);
        b.ComplexProperty(a => a.Creado, Conversiones.Auditoria);
    }
}
