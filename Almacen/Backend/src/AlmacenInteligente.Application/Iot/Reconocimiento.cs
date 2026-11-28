using AlmacenInteligente.Application.Abstracciones;
using AlmacenInteligente.Application.CasosDeUso;
using AlmacenInteligente.Application.Seguridad;
using AlmacenInteligente.Application.Usuarios;
using AlmacenInteligente.Domain.Common;
using AlmacenInteligente.Domain.Iot;
using FluentValidation;

namespace AlmacenInteligente.Application.Iot;

[RequierePermiso(Permisos.Usuarios.Gestionar)]
public sealed record ListarFotosRostro(Guid UsuarioId) : ISolicitud<IReadOnlyList<FotoRostroDto>>;

/// <summary>Foto del rostro para el reconocimiento facial del acceso IoT. JPG o PNG, máximo 4 MB.</summary>
[RequierePermiso(Permisos.Usuarios.Gestionar)]
public sealed record SubirFotoRostro(Guid UsuarioId, string NombreArchivo, string TipoContenido, byte[] Contenido)
    : ISolicitud<FotoRostroDto>;

[RequierePermiso(Permisos.Usuarios.Gestionar)]
public sealed record EliminarFotoRostro(Guid UsuarioId, Guid FotoId) : ISolicitud<bool>;

/// <summary>Entrena el modelo de Custom Vision con las fotos de todos los usuarios y publica la iteración nueva.</summary>
[RequierePermiso(Permisos.Usuarios.Gestionar)]
public sealed record EntrenarModeloRostros : ISolicitud<ResultadoEntrenamientoDto>;

internal sealed class SubirFotoRostroValidador : AbstractValidator<SubirFotoRostro>
{
    public const int TamanoMaximo = 4 * 1024 * 1024;
    private static readonly string[] Tipos = ["image/jpeg", "image/png"];

    public SubirFotoRostroValidador()
    {
        RuleFor(x => x.TipoContenido).Must(Tipos.Contains).WithMessage("La foto debe ser JPG o PNG.");
        RuleFor(x => x.Contenido).Must(c => c.Length is > 0 and <= TamanoMaximo).WithMessage("La foto debe pesar máximo 4 MB.");
    }
}

internal sealed class ReconocimientoManejador(
    IGestorUsuarios usuarios, IFotosRostroRepositorio fotos, IReconocimientoFacial reconocimiento,
    IFileStorage archivos, IUnidadDeTrabajo unidad, IReloj reloj) :
    IManejador<ListarFotosRostro, IReadOnlyList<FotoRostroDto>>,
    IManejador<SubirFotoRostro, FotoRostroDto>,
    IManejador<EliminarFotoRostro, bool>,
    IManejador<EntrenarModeloRostros, ResultadoEntrenamientoDto>
{
    public const string Carpeta = "rostros";

    public async Task<IReadOnlyList<FotoRostroDto>> ManejarAsync(ListarFotosRostro s, CancellationToken ct)
    {
        await CargarUsuarioAsync(s.UsuarioId, ct);
        return (await fotos.ListarAsync(s.UsuarioId, ct)).Select(Dto).ToList();
    }

    public async Task<FotoRostroDto> ManejarAsync(SubirFotoRostro s, CancellationToken ct)
    {
        await CargarUsuarioAsync(s.UsuarioId, ct);
        await using (var contenido = new MemoryStream(s.Contenido))
        {
            var ruta = await archivos.GuardarEnAsync(Carpeta, contenido, s.NombreArchivo, s.TipoContenido, ct);
            var foto = new FotoRostro(s.UsuarioId, ruta, reloj.Ahora);
            if (reconocimiento.Configurado)
            {
                try
                {
                    foto.MarcarSubida(await reconocimiento.SubirImagenAsync(s.UsuarioId.ToString(), s.Contenido, ct));
                }
                catch
                {
                    await archivos.EliminarAsync(ruta, CancellationToken.None);
                    throw;
                }
            }
            fotos.Agregar(foto);
            await unidad.GuardarAsync(ct);
            return Dto(foto);
        }
    }

    public async Task<bool> ManejarAsync(EliminarFotoRostro s, CancellationToken ct)
    {
        var foto = await fotos.ObtenerAsync(s.FotoId, ct);
        if (foto is null || foto.UsuarioId != s.UsuarioId)
            throw new NoEncontradoException("la foto", s.FotoId);

        if (foto.ImagenRemotaId is { } remota && reconocimiento.Configurado)
            await reconocimiento.EliminarImagenesAsync([remota], ct);

        // El modelo publicado todavía conoce la foto borrada: el usuario queda pendiente de reentrenar.
        var restantes = (await fotos.ListarAsync(s.UsuarioId, ct)).Where(f => f.Id != foto.Id).ToList();
        foreach (var f in restantes)
            f.ReiniciarEntrenamiento();
        fotos.Eliminar(foto);
        await unidad.GuardarAsync(ct);

        if (restantes.Count == 0 && reconocimiento.Configurado)
            await reconocimiento.EliminarEtiquetaAsync(s.UsuarioId.ToString(), ct);
        await archivos.EliminarAsync(foto.Ruta, ct);
        return true;
    }

    public async Task<ResultadoEntrenamientoDto> ManejarAsync(EntrenarModeloRostros s, CancellationToken ct)
    {
        if (!reconocimiento.Configurado)
            throw new ReconocimientoNoDisponibleException(
                "Custom Vision no está configurado: llena la sección CustomVision (endpoints, claves y proyecto).");

        var todas = await fotos.ListarTodasAsync(ct);
        var porUsuario = todas.GroupBy(f => f.UsuarioId).ToList();
        await ValidarEntrenableAsync(porUsuario, ct);

        // Fotos que se cargaron cuando Custom Vision no estaba configurado.
        foreach (var foto in todas.Where(f => f.ImagenRemotaId is null))
        {
            await using var flujo = await archivos.AbrirAsync(foto.Ruta, ct)
                ?? throw new DomainException($"No se encontró el archivo de una foto ({foto.Ruta}); elimínala y vuelve a subirla.");
            using var memoria = new MemoryStream();
            await flujo.CopyToAsync(memoria, ct);
            foto.MarcarSubida(await reconocimiento.SubirImagenAsync(foto.UsuarioId.ToString(), memoria.ToArray(), ct));
            await unidad.GuardarAsync(ct);
        }

        var iteracion = await reconocimiento.EntrenarYPublicarAsync(ct);
        var ahora = reloj.Ahora;
        foreach (var foto in todas)
            foto.MarcarEntrenada(ahora);
        await unidad.GuardarAsync(ct);
        return new ResultadoEntrenamientoDto(iteracion, porUsuario.Count, todas.Count);
    }

    /// <summary>Custom Vision exige al menos 2 etiquetas y que cada etiqueta tenga 5 imágenes o más.</summary>
    private async Task ValidarEntrenableAsync(IReadOnlyList<IGrouping<Guid, FotoRostro>> porUsuario, CancellationToken ct)
    {
        var incompletos = porUsuario.Where(g => g.Count() < FotoRostro.MinimoPorUsuario).ToList();
        if (incompletos.Count > 0)
        {
            var nombres = new List<string>();
            foreach (var g in incompletos)
                nombres.Add($"{(await usuarios.ObtenerAsync(g.Key, ct))?.Nombre ?? g.Key.ToString()} ({g.Count()})");
            throw new DomainException(
                $"Cada usuario con fotos necesita al menos {FotoRostro.MinimoPorUsuario} para entrenar. " +
                $"Completa o elimina las fotos de: {string.Join(", ", nombres)}.");
        }
        if (porUsuario.Count < 2)
            throw new DomainException(
                $"Para entrenar se necesitan al menos 2 usuarios con {FotoRostro.MinimoPorUsuario} fotos o más (hay {porUsuario.Count}).");
    }

    private async Task CargarUsuarioAsync(Guid id, CancellationToken ct) =>
        _ = await usuarios.ObtenerAsync(id, ct) ?? throw new NoEncontradoException("el usuario", id);

    private static FotoRostroDto Dto(FotoRostro f) => new(f.Id, f.Ruta, f.SubidaEn, f.ImagenRemotaId is not null, f.EntrenadaEn is not null);
}
