using AlmacenInteligente.Application.Abstracciones;
using AlmacenInteligente.Application.CasosDeUso;
using AlmacenInteligente.Application.Clientes;
using AlmacenInteligente.Application.Seguridad;
using AlmacenInteligente.Application.Terceros;
using AlmacenInteligente.Domain.Common;
using AlmacenInteligente.Domain.Terceros;
using Microsoft.Extensions.DependencyInjection;

namespace AlmacenInteligente.Application.Tests;

public class MediadorTests
{
    private static readonly DireccionDto Direccion =
        new("Av. Reforma", "100", null, "Juárez", "06600", "Cuauhtémoc", "Ciudad de México");

    private static DatosTercero Datos(string codigo = "cli-1", string rfc = "CNO010203AB1", string nombre = "Comercial del Norte") =>
        new(codigo, nombre, "compras@norte.mx", rfc, Direccion);

    private static CrearCliente Solicitud(string codigo = "cli-1", string rfc = "CNO010203AB1") => new(Datos(codigo, rfc));

    private sealed class Usuario(string? rol) : IUsuarioActual
    {
        public Guid? Id { get; } = rol is null ? null : Guid.NewGuid();
        public bool EstaAutenticado => rol is not null;
        public bool TieneRol(string r) => r == rol;
        public bool TienePermiso(string permiso) => rol is not null && MatrizPermisos.PorDefecto[rol].Contains(permiso);
    }

    private sealed class Reloj : IReloj
    {
        public DateTimeOffset Ahora => new(2026, 10, 22, 10, 0, 0, TimeSpan.Zero);
        public DateOnly Hoy => DateOnly.FromDateTime(Ahora.Date);
    }

    private sealed class ClientesEnMemoria : IClienteRepositorio, IUnidadDeTrabajo
    {
        public List<Cliente> Guardados { get; } = [];
        private readonly List<Cliente> _pendientes = [];

        public Task<Cliente?> ObtenerAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Guardados.SingleOrDefault(c => c.Id == id));

        public void Agregar(Cliente entidad) => _pendientes.Add(entidad);

        public Task<bool> ExisteCodigoAsync(string codigo, CancellationToken ct = default) =>
            Task.FromResult(Guardados.Any(c => c.Codigo == codigo));

        public Task<(IReadOnlyList<Cliente> Elementos, int Total)> BuscarAsync(
            string? texto, bool incluirInactivos, int numeroPagina, int tamano, CancellationToken ct = default)
        {
            var filtrados = Guardados
                .Where(c => incluirInactivos || c.Activo)
                .Where(c => texto is null || c.Nombre.Contains(texto, StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c.Nombre)
                .ToList();
            IReadOnlyList<Cliente> pagina = filtrados.Skip((numeroPagina - 1) * tamano).Take(tamano).ToList();
            return Task.FromResult((pagina, filtrados.Count));
        }

        public Task GuardarAsync(CancellationToken ct = default)
        {
            Guardados.AddRange(_pendientes);
            _pendientes.Clear();
            return Task.CompletedTask;
        }
    }

    private static (IMediador Mediador, ClientesEnMemoria Repo) Crear(string? rol, ClientesEnMemoria? repo = null)
    {
        repo ??= new ClientesEnMemoria();
        var services = new ServiceCollection()
            .AddApplication()
            .AddSingleton<IUsuarioActual>(new Usuario(rol))
            .AddSingleton<IReloj, Reloj>()
            .AddSingleton<IClienteRepositorio>(repo)
            .AddSingleton<IUnidadDeTrabajo>(repo)
            .BuildServiceProvider();
        return (services.CreateScope().ServiceProvider.GetRequiredService<IMediador>(), repo);
    }

    [Fact]
    public async Task Agente_de_ventas_crea_cliente()
    {
        var (mediador, repo) = Crear(Roles.AgenteVentas);

        var cliente = await mediador.EnviarAsync(Solicitud());

        Assert.Equal("CLI-1", cliente.Codigo);
        Assert.Single(repo.Guardados);
        Assert.Equal(cliente, await mediador.EnviarAsync(new ObtenerCliente(cliente.Id)));
    }

    [Theory]
    [InlineData(Roles.EncargadoRecepciones)]
    [InlineData(Roles.Gerente)]
    public async Task Sin_permiso_se_deniega_antes_de_tocar_datos(string rol)
    {
        var (mediador, repo) = Crear(rol);
        await Assert.ThrowsAsync<AccesoDenegadoException>(() => mediador.EnviarAsync(Solicitud()));
        Assert.Empty(repo.Guardados);
    }

    [Fact]
    public async Task Sin_sesion_se_exige_autenticacion()
    {
        var (mediador, _) = Crear(null);
        await Assert.ThrowsAsync<NoAutenticadoException>(() => mediador.EnviarAsync(Solicitud()));
    }

    [Fact]
    public async Task Validacion_de_forma_reporta_campos()
    {
        var (mediador, _) = Crear(Roles.Superadmin);
        var error = await Assert.ThrowsAsync<ValidacionException>(() => mediador.EnviarAsync(Solicitud(codigo: "")));
        Assert.Contains("Datos.Codigo", error.Errores.Keys);
    }

    [Fact]
    public async Task Reglas_de_dominio_llegan_como_DomainException()
    {
        var (mediador, _) = Crear(Roles.Superadmin);
        await Assert.ThrowsAsync<DomainException>(() => mediador.EnviarAsync(Solicitud(rfc: "MALO")));

        await mediador.EnviarAsync(Solicitud());
        await Assert.ThrowsAsync<DomainException>(() => mediador.EnviarAsync(Solicitud()));
    }

    [Fact]
    public async Task Cliente_inexistente()
    {
        var (mediador, _) = Crear(Roles.Gerente);
        await Assert.ThrowsAsync<NoEncontradoException>(() => mediador.EnviarAsync(new ObtenerCliente(Guid.NewGuid())));
    }

    [Fact]
    public async Task Solo_superadmin_edita_y_desactivar_oculta_del_listado()
    {
        var (admin, repo) = Crear(Roles.Superadmin);
        var cliente = await admin.EnviarAsync(Solicitud());
        var (agente, _) = Crear(Roles.AgenteVentas, repo);

        await Assert.ThrowsAsync<AccesoDenegadoException>(() =>
            agente.EnviarAsync(new EditarCliente(cliente.Id, Datos(nombre: "Otro"))));

        var editado = await admin.EnviarAsync(new EditarCliente(cliente.Id, Datos(codigo: "IGNORADO", nombre: "Comercial del Norte SA")));
        Assert.Equal("Comercial del Norte SA", editado.Nombre);
        Assert.Equal("CLI-1", editado.Codigo);

        await admin.EnviarAsync(new CambiarEstadoCliente(cliente.Id, Activo: false));
        Assert.Equal(0, (await agente.EnviarAsync(new ListarClientes())).Total);
        Assert.Equal(1, (await agente.EnviarAsync(new ListarClientes(IncluirInactivos: true))).Total);
    }
}
