using AlmacenInteligente.Application.Abstracciones;
using AlmacenInteligente.Application.CasosDeUso;
using AlmacenInteligente.Application.Seguridad;
using AlmacenInteligente.Application.Terceros;
using AlmacenInteligente.Domain.Common;
using AlmacenInteligente.Domain.Terceros;
using AlmacenInteligente.Domain.ValueObjects;
using FluentValidation;

namespace AlmacenInteligente.Application.Clientes;

[RequierePermiso(Permisos.Clientes.Ver)]
public sealed record ListarClientes(string? Busqueda = null, bool IncluirInactivos = false, int NumeroPagina = 1, int Tamano = 20)
    : ISolicitud<Pagina<TerceroDto>>;

[RequierePermiso(Permisos.Clientes.Ver)]
public sealed record ObtenerCliente(Guid Id) : ISolicitud<TerceroDto>;

[RequierePermiso(Permisos.Clientes.Crear)]
public sealed record CrearCliente(DatosTercero Datos) : ISolicitud<TerceroDto>;

[RequierePermiso(Permisos.Clientes.Editar)]
public sealed record EditarCliente(Guid Id, DatosTercero Datos) : ISolicitud<TerceroDto>;

[RequierePermiso(Permisos.Clientes.Editar)]
public sealed record CambiarEstadoCliente(Guid Id, bool Activo) : ISolicitud<TerceroDto>;

internal sealed class CrearClienteValidador : AbstractValidator<CrearCliente>
{
    public CrearClienteValidador() => RuleFor(x => x.Datos).NotNull().SetValidator(new DatosTerceroValidador());
}

internal sealed class EditarClienteValidador : AbstractValidator<EditarCliente>
{
    public EditarClienteValidador() => RuleFor(x => x.Datos).NotNull().SetValidator(new DatosTerceroValidador());
}

internal sealed class ClientesManejador(
    IClienteRepositorio clientes, IUnidadDeTrabajo unidad, IUsuarioActual usuario, IReloj reloj) :
    IManejador<ListarClientes, Pagina<TerceroDto>>,
    IManejador<ObtenerCliente, TerceroDto>,
    IManejador<CrearCliente, TerceroDto>,
    IManejador<EditarCliente, TerceroDto>,
    IManejador<CambiarEstadoCliente, TerceroDto>
{
    public async Task<Pagina<TerceroDto>> ManejarAsync(ListarClientes s, CancellationToken ct)
    {
        var (elementos, total) = await clientes.BuscarAsync(s.Busqueda, s.IncluirInactivos, s.NumeroPagina, s.Tamano, ct);
        return new Pagina<TerceroDto>(elementos.Select(TerceroDto.DeDominio).ToList(), total, s.NumeroPagina, s.Tamano);
    }

    public async Task<TerceroDto> ManejarAsync(ObtenerCliente s, CancellationToken ct) =>
        TerceroDto.DeDominio(await Cargar(s.Id, ct));

    public async Task<TerceroDto> ManejarAsync(CrearCliente s, CancellationToken ct)
    {
        var d = s.Datos;
        if (await clientes.ExisteCodigoAsync(d.Codigo.Trim().ToUpperInvariant(), ct))
            throw new DomainException($"Ya existe un cliente con el código {d.Codigo.Trim().ToUpperInvariant()}.");

        var cliente = new Cliente(d.Codigo, d.Nombre, Correo.Crear(d.Correo), Rfc.Crear(d.Rfc), d.Direccion.ADominio(),
            new Auditoria(usuario.Id!.Value, reloj.Ahora));
        clientes.Agregar(cliente);
        await unidad.GuardarAsync(ct);
        return TerceroDto.DeDominio(cliente);
    }

    /// <summary>El código no se edita: identifica al cliente en documentos ya emitidos.</summary>
    public async Task<TerceroDto> ManejarAsync(EditarCliente s, CancellationToken ct)
    {
        var cliente = await Cargar(s.Id, ct);
        var d = s.Datos;
        cliente.Actualizar(d.Nombre, Correo.Crear(d.Correo), Rfc.Crear(d.Rfc), d.Direccion.ADominio());
        await unidad.GuardarAsync(ct);
        return TerceroDto.DeDominio(cliente);
    }

    public async Task<TerceroDto> ManejarAsync(CambiarEstadoCliente s, CancellationToken ct)
    {
        var cliente = await Cargar(s.Id, ct);
        if (s.Activo) cliente.Activar(); else cliente.Desactivar();
        await unidad.GuardarAsync(ct);
        return TerceroDto.DeDominio(cliente);
    }

    private async Task<Cliente> Cargar(Guid id, CancellationToken ct) =>
        await clientes.ObtenerAsync(id, ct) ?? throw new NoEncontradoException("el cliente", id);
}
