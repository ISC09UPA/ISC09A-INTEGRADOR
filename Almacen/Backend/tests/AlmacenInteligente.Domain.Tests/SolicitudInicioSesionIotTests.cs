using AlmacenInteligente.Domain.Common;
using AlmacenInteligente.Domain.Iot;

namespace AlmacenInteligente.Domain.Tests;

public class SolicitudInicioSesionIotTests
{
    private static readonly DateTimeOffset Inicio = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Usuario = Guid.NewGuid();

    [Fact]
    public void Nace_pendiente_y_vence_a_los_60_segundos()
    {
        var s = new SolicitudInicioSesionIot(Inicio);

        Assert.Equal(Inicio.AddSeconds(60), s.Expira);
        Assert.Equal(EstadoSolicitudInicioSesion.Pendiente, s.EstadoEn(Inicio.AddSeconds(60)));
        Assert.Equal(EstadoSolicitudInicioSesion.Expirada, s.EstadoEn(Inicio.AddSeconds(61)));
    }

    [Fact]
    public void El_id_es_aleatorio_y_no_secuencial()
    {
        var a = new SolicitudInicioSesionIot(Inicio);
        var b = new SolicitudInicioSesionIot(Inicio);

        Assert.NotEqual(a.Id, b.Id);
        Assert.Equal(4, a.Id.Version);
    }

    [Fact]
    public void Solo_acepta_accesos_posteriores_a_su_creacion_y_dentro_de_la_vigencia()
    {
        var s = new SolicitudInicioSesionIot(Inicio);

        Assert.False(s.AceptaAcceso(Inicio.AddSeconds(-1)));
        Assert.True(s.AceptaAcceso(Inicio.AddSeconds(30)));
        Assert.False(s.AceptaAcceso(Inicio.AddSeconds(61)));
        Assert.Throws<DomainException>(() => s.Completar(Usuario, Inicio.AddSeconds(61)));
    }

    [Fact]
    public void Completada_entrega_el_usuario_una_sola_vez()
    {
        var s = new SolicitudInicioSesionIot(Inicio);
        s.Completar(Usuario, Inicio.AddSeconds(10));

        Assert.Equal(EstadoSolicitudInicioSesion.Completada, s.EstadoEn(Inicio.AddMinutes(5)));
        Assert.Equal(Usuario, s.Consumir());
        Assert.Null(s.Consumir());
        Assert.Equal(EstadoSolicitudInicioSesion.Consumida, s.Estado);
    }

    [Fact]
    public void Negada_guarda_el_motivo_y_ya_no_se_puede_completar()
    {
        var s = new SolicitudInicioSesionIot(Inicio);
        s.Negar("Rostro no coincide", Inicio.AddSeconds(5));

        Assert.Equal(EstadoSolicitudInicioSesion.Negada, s.EstadoEn(Inicio.AddSeconds(6)));
        Assert.Equal("Rostro no coincide", s.Motivo);
        Assert.Null(s.Consumir());
        Assert.Throws<DomainException>(() => s.Completar(Usuario, Inicio.AddSeconds(7)));
    }

    [Fact]
    public void Cancelar_solo_aplica_si_sigue_pendiente()
    {
        var pendiente = new SolicitudInicioSesionIot(Inicio);
        Assert.True(pendiente.Cancelar());
        Assert.False(pendiente.AceptaAcceso(Inicio.AddSeconds(1)));

        var completada = new SolicitudInicioSesionIot(Inicio);
        completada.Completar(Usuario, Inicio.AddSeconds(1));
        Assert.False(completada.Cancelar());
        Assert.Equal(EstadoSolicitudInicioSesion.Completada, completada.Estado);
    }
}
