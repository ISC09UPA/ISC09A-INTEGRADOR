using AlmacenInteligente.Infrastructure.Mensajeria;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlmacenInteligente.Infrastructure.Persistencia.Configuraciones;

internal sealed class OutboxConfiguracion : IEntityTypeConfiguration<MensajeOutbox>
{
    public void Configure(EntityTypeBuilder<MensajeOutbox> b)
    {
        b.ToTable("outbox");
        b.HasKey(m => m.Id);
        b.Property(m => m.Tipo).HasMaxLength(300).IsRequired();
        b.Property(m => m.Contenido).HasColumnType("jsonb").IsRequired();
        b.Property(m => m.UltimoError).HasMaxLength(2000);
        // Solo los pendientes: el procesador los busca en orden de llegada.
        b.HasIndex(m => m.OcurrioEn).HasFilter("procesado_en IS NULL");
    }
}
