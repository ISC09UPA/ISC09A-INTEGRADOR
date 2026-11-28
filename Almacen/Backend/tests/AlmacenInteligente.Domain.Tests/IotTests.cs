using AlmacenInteligente.Domain.Common;
using AlmacenInteligente.Domain.Iot;

namespace AlmacenInteligente.Domain.Tests;

public class ReglaAccesoTests
{
    private static readonly Guid Dueno = Guid.NewGuid();
    private static readonly Guid Otro = Guid.NewGuid();

    private static PrediccionRostro P(Guid usuario, double probabilidad) => new(usuario.ToString(), probabilidad);

    [Fact]
    public void Permite_si_la_etiqueta_mas_probable_es_del_dueno_y_alcanza_el_umbral()
    {
        var decision = ReglaAcceso.Decidir(Dueno, [P(Otro, 0.05), P(Dueno, 0.93)], 0.8);

        Assert.True(decision.Permitido);
        Assert.Equal(0.93, decision.Confianza);
    }

    [Fact]
    public void El_umbral_es_inclusivo()
    {
        Assert.True(ReglaAcceso.Decidir(Dueno, [P(Dueno, 0.8)], 0.8).Permitido);
    }

    [Fact]
    public void Niega_si_la_confianza_no_alcanza_el_umbral()
    {
        var decision = ReglaAcceso.Decidir(Dueno, [P(Dueno, 0.79), P(Otro, 0.21)], 0.8);

        Assert.False(decision.Permitido);
        Assert.Contains("Confianza insuficiente", decision.Motivo);
    }

    [Fact]
    public void Niega_si_el_rostro_mas_probable_es_de_otro_aunque_el_dueno_pase_el_umbral()
    {
        // Probabilidades multiclase raras pero posibles si el modelo duda: gana la mayor.
        var decision = ReglaAcceso.Decidir(Dueno, [P(Dueno, 0.85), P(Otro, 0.9)], 0.8);

        Assert.False(decision.Permitido);
        Assert.Equal(0.85, decision.Confianza);
        Assert.Contains("no coincide", decision.Motivo);
    }

    [Fact]
    public void Compara_la_etiqueta_sin_importar_mayusculas()
    {
        var decision = ReglaAcceso.Decidir(Dueno, [new PrediccionRostro(Dueno.ToString().ToUpperInvariant(), 0.99)], 0.8);
        Assert.True(decision.Permitido);
    }

    [Fact]
    public void Sin_predicciones_se_niega()
    {
        Assert.False(ReglaAcceso.Decidir(Dueno, [], 0.8).Permitido);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1.5)]
    public void Umbral_fuera_de_rango_es_error_de_configuracion(double umbral) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ReglaAcceso.Decidir(Dueno, [P(Dueno, 1)], umbral));
}

public class CodigoNfcTests
{
    [Theory]
    [InlineData("04:a2:1b:7c", "04A21B7C")]
    [InlineData(" 04-A2-1B-7C ", "04A21B7C")]
    [InlineData("04 a2 1b 7c", "04A21B7C")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Normaliza_el_uid(string? entrada, string? esperado) =>
        Assert.Equal(esperado, CodigoNfc.Normalizar(entrada));

    [Fact]
    public void Rechaza_caracteres_que_no_son_hexadecimales() =>
        Assert.Throws<DomainException>(() => CodigoNfc.Normalizar("04:ZZ"));

    [Fact]
    public void Producto_guarda_la_etiqueta_normalizada()
    {
        var producto = Fabrica.Producto();
        producto.AsignarCodigoNfc("de:ad:be:ef");
        Assert.Equal("DEADBEEF", producto.CodigoNfc);
        producto.AsignarCodigoNfc("");
        Assert.Null(producto.CodigoNfc);
    }

    [Theory]
    [InlineData(0, 0, EstadoReconocimiento.SinFotos)]
    [InlineData(4, 0, EstadoReconocimiento.FaltanFotos)]
    [InlineData(5, 0, EstadoReconocimiento.PendienteDeEntrenar)]
    [InlineData(6, 5, EstadoReconocimiento.PendienteDeEntrenar)]
    [InlineData(5, 5, EstadoReconocimiento.Entrenado)]
    public void Estado_del_reconocimiento(int fotos, int entrenadas, EstadoReconocimiento esperado) =>
        Assert.Equal(esperado, FotoRostro.Estado(fotos, entrenadas));
}

public class SesionVentaInteligenteTests
{
    private static readonly TimeSpan Limite = TimeSpan.FromMinutes(2);
    private static readonly Guid Ana = Guid.NewGuid();
    private static readonly Guid Beto = Guid.NewGuid();
    private static readonly Guid Almacen = Guid.NewGuid();
    private static readonly DateTimeOffset T0 = Fabrica.Ahora;

    [Fact]
    public void Libre_se_puede_tomar_y_queda_vigente()
    {
        var candado = new SesionVentaInteligente();
        candado.Tomar(Ana, Almacen, T0, Limite);

        Assert.True(candado.EstaVigente(T0.AddSeconds(30), Limite));
        Assert.Equal(Ana, candado.UsuarioId);
        Assert.NotNull(candado.SesionId);
    }

    [Fact]
    public void Otro_usuario_no_puede_tomarlo_mientras_esta_vigente()
    {
        var candado = new SesionVentaInteligente();
        candado.Tomar(Ana, Almacen, T0, Limite);

        Assert.False(candado.PuedeTomar(Beto, T0.AddMinutes(1), Limite));
        Assert.Throws<DomainException>(() => candado.Tomar(Beto, Almacen, T0.AddMinutes(1), Limite));
    }

    [Fact]
    public void Vence_sin_latidos_y_entonces_otro_lo_toma_con_sesion_nueva()
    {
        var candado = new SesionVentaInteligente();
        candado.Tomar(Ana, Almacen, T0, Limite);
        var sesionAna = candado.SesionId;

        var despues = T0.AddMinutes(2).AddSeconds(1);
        Assert.False(candado.EstaVigente(despues, Limite));
        candado.Tomar(Beto, Almacen, despues, Limite);

        Assert.Equal(Beto, candado.UsuarioId);
        Assert.NotEqual(sesionAna, candado.SesionId);
    }

    [Fact]
    public void El_latido_lo_mantiene_vigente()
    {
        var candado = new SesionVentaInteligente();
        candado.Tomar(Ana, Almacen, T0, Limite);
        Assert.True(candado.Latido(Ana, T0.AddMinutes(1.5)));

        Assert.True(candado.EstaVigente(T0.AddMinutes(3), Limite));
        Assert.False(candado.PuedeTomar(Beto, T0.AddMinutes(3), Limite));
    }

    [Fact]
    public void Solo_el_dueno_late_y_libera()
    {
        var candado = new SesionVentaInteligente();
        candado.Tomar(Ana, Almacen, T0, Limite);

        Assert.False(candado.Latido(Beto, T0.AddSeconds(10)));
        candado.Liberar(Beto);
        Assert.Equal(Ana, candado.UsuarioId);

        candado.Liberar(Ana);
        Assert.Null(candado.UsuarioId);
        Assert.True(candado.PuedeTomar(Beto, T0.AddSeconds(20), Limite));
    }

    [Fact]
    public void Retomarlo_el_mismo_usuario_conserva_la_sesion()
    {
        var candado = new SesionVentaInteligente();
        candado.Tomar(Ana, Almacen, T0, Limite);
        var sesion = candado.SesionId;

        candado.Tomar(Ana, Guid.NewGuid(), T0.AddSeconds(30), Limite);
        Assert.Equal(sesion, candado.SesionId);
    }
}
