using AlmacenInteligente.Domain.Inventario;

namespace AlmacenInteligente.Domain.Tests;

public class SemaforoStockTests
{
    [Theory]
    [InlineData(0, 5, NivelStock.Rojo)]
    [InlineData(0, 0, NivelStock.Rojo)]
    [InlineData(3, 5, NivelStock.Amarillo)]
    [InlineData(5, 5, NivelStock.Amarillo)]
    [InlineData(6, 5, NivelStock.Verde)]
    [InlineData(1, 0, NivelStock.Verde)]
    public void El_nivel_sigue_existencia_y_minimo(decimal cantidad, decimal minimo, NivelStock esperado) =>
        Assert.Equal(esperado, SemaforoStock.Nivel(cantidad, minimo));
}
