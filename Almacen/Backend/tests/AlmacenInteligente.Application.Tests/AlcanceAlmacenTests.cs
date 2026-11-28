using AlmacenInteligente.Application.Consultas;
using AlmacenInteligente.Application.Seguridad;
using AlmacenInteligente.Application.Usuarios;
using AlmacenInteligente.Domain.Common;

namespace AlmacenInteligente.Application.Tests;

public class AlcanceAlmacenTests
{
    private static readonly Guid Central = Guid.NewGuid();
    private static readonly Guid Norte = Guid.NewGuid();

    private static AlcanceAlmacen Para(string rol, Guid? almacenId)
    {
        var id = Guid.NewGuid();
        var usuarios = new GestorUsuariosEnMemoria();
        usuarios.Usuarios.Add(new UsuarioDto(id, "Ana", "ana@x.mx", rol, true, AlmacenId: almacenId));
        return new AlcanceAlmacen(new UsuarioDePrueba(id, rol), usuarios);
    }

    [Fact]
    public async Task Un_agente_solo_opera_su_almacen()
    {
        var alcance = Para(Roles.AgenteVentas, Central);
        Assert.Equal(Central, await alcance.AlmacenAsync());
        await alcance.AsegurarAsync(Central);
        var error = await Assert.ThrowsAsync<DomainException>(() => alcance.AsegurarAsync(Norte));
        Assert.Equal(AlcanceAlmacen.OtroAlmacen, error.Message);
    }

    [Fact]
    public async Task El_superadmin_ve_todos()
    {
        var alcance = Para(Roles.Superadmin, null);
        Assert.Null(await alcance.AlmacenAsync());
        await alcance.AsegurarAsync(Norte);
    }

    [Fact]
    public async Task Sin_almacen_asignado_no_puede_operar()
    {
        var alcance = Para(Roles.Gerente, null);
        var error = await Assert.ThrowsAsync<DomainException>(() => alcance.AlmacenAsync());
        Assert.Equal(AlcanceAlmacen.SinAlmacen, error.Message);
    }

    [Fact]
    public void Deja_solo_la_existencia_de_su_almacen()
    {
        var producto = new ProductoOpcionDto(Guid.NewGuid(), "TOR-001", "Tornillo", "PZA", false,
            [new ExistenciaOpcionDto(Central, "Central", 5, 1), new ExistenciaOpcionDto(Norte, "Norte", 7, 1)]);
        var filtrado = Assert.Single(AlcanceAlmacen.Filtrar([producto], Central));
        Assert.Equal(Central, Assert.Single(filtrado.Existencias).AlmacenId);
    }
}
