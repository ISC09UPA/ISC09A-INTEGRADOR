using System.Reflection;
using AlmacenInteligente.Application.CasosDeUso;
using AlmacenInteligente.Application.Seguridad;
using P = AlmacenInteligente.Application.Seguridad.Permisos;

namespace AlmacenInteligente.Application.Tests;

public class PermisosTests
{
    private static bool Puede(string rol, string permiso) => MatrizPermisos.PorDefecto[rol].Contains(permiso);

    [Fact]
    public void Cada_rol_tiene_matriz_y_solo_usa_permisos_declarados()
    {
        Assert.Equal(Roles.Todos.Order(), MatrizPermisos.PorDefecto.Keys.Order());
        foreach (var permisos in MatrizPermisos.PorDefecto.Values)
            Assert.All(permisos, p => Assert.Contains(p, P.Todos));
    }

    [Fact]
    public void Permisos_no_se_repiten() =>
        Assert.Equal(P.Todos.Count, P.Todos.Distinct().Count());

    [Theory]
    // Sección 4 del plan
    [InlineData(Roles.Superadmin, P.Usuarios.Gestionar, true)]
    [InlineData(Roles.Gerente, P.Usuarios.Gestionar, false)]
    [InlineData(Roles.AgenteVentas, P.Clientes.Crear, true)]
    [InlineData(Roles.AgenteVentas, P.Ventas.Cancelar, true)]
    [InlineData(Roles.AgenteVentas, P.Recepciones.Crear, false)]
    [InlineData(Roles.AgenteVentas, P.Almacenes.VerExistencias, true)]
    [InlineData(Roles.EncargadoRecepciones, P.Recepciones.Crear, true)]
    [InlineData(Roles.EncargadoRecepciones, P.Recepciones.Cancelar, false)]
    [InlineData(Roles.EncargadoRecepciones, P.Productos.Aprobar, false)]
    [InlineData(Roles.EncargadoRecepciones, P.Traspasos.Crear, true)]
    [InlineData(Roles.EncargadoRecepciones, P.Ventas.Crear, false)]
    [InlineData(Roles.Gerente, P.Productos.Aprobar, true)]
    [InlineData(Roles.Gerente, P.Recepciones.Cancelar, true)]
    [InlineData(Roles.Gerente, P.Ventas.Crear, true)]
    [InlineData(Roles.Gerente, P.Reportes.Ver, true)]
    [InlineData(Roles.Gerente, P.Clientes.Crear, false)]
    [InlineData(Roles.Gerente, P.Proveedores.Editar, false)]
    [InlineData(Roles.Superadmin, P.Alertas.RecibirBajoStock, false)]
    [InlineData(Roles.Gerente, P.Alertas.RecibirBajoStock, true)]
    [InlineData(Roles.EncargadoRecepciones, P.Alertas.RecibirBajoStock, true)]
    [InlineData(Roles.Superadmin, P.Api.Acceder, true)]
    [InlineData(Roles.Gerente, P.Api.Acceder, true)]
    [InlineData(Roles.AgenteVentas, P.Api.Acceder, false)]
    [InlineData(Roles.EncargadoRecepciones, P.Api.Acceder, false)]
    public void Matriz_del_plan(string rol, string permiso, bool esperado) =>
        Assert.Equal(esperado, Puede(rol, permiso));

    [Fact]
    public void Toda_solicitud_declara_permiso_o_es_publica()
    {
        var solicitudes = typeof(IMediador).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISolicitud<>)))
            .ToList();

        Assert.NotEmpty(solicitudes);
        Assert.All(solicitudes, t =>
        {
            var permiso = t.GetCustomAttribute<RequierePermisoAttribute>()?.Permiso;
            Assert.True(permiso is not null || t.GetCustomAttribute<PublicaAttribute>() is not null,
                $"{t.Name} no declara [RequierePermiso] ni [Publica].");
            if (permiso is not null)
                Assert.Contains(permiso, P.Todos);
        });
    }
}
