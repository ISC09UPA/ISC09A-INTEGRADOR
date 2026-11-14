using AlmacenInteligente.Domain.Common;
using AlmacenInteligente.Domain.ValueObjects;

namespace AlmacenInteligente.Domain.Tests;

public class ValueObjectsTests
{
    [Theory]
    [InlineData("CNO010203AB1", false)]
    [InlineData("GODE561231GR8", true)]
    [InlineData(" gode-561231-gr8 ", true)]
    [InlineData("XAXX010101000", true)]
    public void Rfc_valido(string valor, bool esFisica)
    {
        var rfc = Rfc.Crear(valor);
        Assert.Equal(esFisica, rfc.EsPersonaFisica);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ABC")]
    [InlineData("CNO011303AB1")] // mes 13
    [InlineData("1NO010203AB1")]
    public void Rfc_invalido(string valor) =>
        Assert.Throws<DomainException>(() => Rfc.Crear(valor));

    [Fact]
    public void Correo_se_normaliza_a_minusculas() =>
        Assert.Equal("ana@empresa.mx", Correo.Crear(" Ana@Empresa.MX ").Valor);

    [Theory]
    [InlineData("sin-arroba")]
    [InlineData("Ana <ana@empresa.mx>")]
    public void Correo_invalido(string valor) =>
        Assert.Throws<DomainException>(() => Correo.Crear(valor));

    [Fact]
    public void Codigo_postal_mexicano_requiere_5_digitos() =>
        Assert.Throws<DomainException>(() => new Direccion("Calle", "1", null, "Centro", "123", "Mty", "NL"));

    [Fact]
    public void Codigo_postal_extranjero_es_libre()
    {
        var d = new Direccion("Main St", "1", null, "Downtown", "TX 78701", "Austin", "Texas", "Estados Unidos");
        Assert.Equal("Estados Unidos", d.Pais);
    }

    [Fact]
    public void Importe_de_linea_redondea_subtotal_e_iva_a_2_decimales()
    {
        // 3 × 10.335 = 31.005 → 31.01; IVA 31.01 × 0.16 = 4.9616 → 4.96
        var importe = Importe.DeLinea(3, 10.335m, TasaIva.General);
        Assert.Equal(31.01m, importe.Subtotal.Monto);
        Assert.Equal(4.96m, importe.Iva.Monto);
        Assert.Equal(35.97m, importe.Total.Monto);
    }

    [Fact]
    public void Tasa_iva_fuera_de_rango() =>
        Assert.Throws<DomainException>(() => new TasaIva(16m));
}
