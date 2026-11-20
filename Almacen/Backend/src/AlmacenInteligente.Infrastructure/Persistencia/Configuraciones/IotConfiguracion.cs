using AlmacenInteligente.Domain.Iot;
using AlmacenInteligente.Domain.Productos;
using AlmacenInteligente.Infrastructure.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlmacenInteligente.Infrastructure.Persistencia.Configuraciones;

internal sealed class FotoRostroConfiguracion : IEntityTypeConfiguration<FotoRostro>
{
    public void Configure(EntityTypeBuilder<FotoRostro> b)
    {
        b.ToTable("iot_fotos_rostro");
        b.HasKey(f => f.Id);
        b.Property(f => f.Ruta).HasMaxLength(400);
        b.Property(f => f.ImagenRemotaId).HasMaxLength(100);
        b.HasIndex(f => f.UsuarioId);
        b.HasOne<Usuario>().WithMany().HasForeignKey(f => f.UsuarioId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AccesoIotConfiguracion : IEntityTypeConfiguration<AccesoIot>
{
    public void Configure(EntityTypeBuilder<AccesoIot> b)
    {
        b.ToTable("iot_accesos");
        b.HasKey(a => a.Id);
        b.Property(a => a.Nfc).HasMaxLength(CodigoNfc.LongitudMaxima);
        b.Property(a => a.Motivo).HasMaxLength(300);
        // Sin FK al usuario: la bitácora se conserva aunque el usuario cambie.
        b.HasIndex(a => a.Fecha).IsDescending();
    }
}

internal sealed class SesionVentaInteligenteConfiguracion : IEntityTypeConfiguration<SesionVentaInteligente>
{
    public void Configure(EntityTypeBuilder<SesionVentaInteligente> b)
    {
        b.ToTable("iot_sesion_venta", t => t.HasCheckConstraint("ck_iot_sesion_venta_unica", $"id = {SesionVentaInteligente.IdUnico}"));
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).ValueGeneratedNever();
    }
}

internal sealed class EscaneoIotConfiguracion : IEntityTypeConfiguration<EscaneoIot>
{
    public void Configure(EntityTypeBuilder<EscaneoIot> b)
    {
        b.ToTable("iot_escaneos");
        b.HasKey(e => e.Id);
        b.HasIndex(e => e.SesionId).HasFilter("NOT procesado");
        b.HasOne<Producto>().WithMany().HasForeignKey(e => e.ProductoId).OnDelete(DeleteBehavior.Cascade);
    }
}
