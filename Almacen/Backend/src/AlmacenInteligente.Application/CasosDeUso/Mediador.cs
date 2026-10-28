using System.Collections.Concurrent;
using System.Reflection;
using AlmacenInteligente.Application.Abstracciones;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace AlmacenInteligente.Application.CasosDeUso;

/// <summary>Un comando o consulta. Blazor y la API envían exactamente estos objetos al mismo <see cref="IMediador"/>.</summary>
public interface ISolicitud<TResultado>;

public interface IManejador<in TSolicitud, TResultado> where TSolicitud : ISolicitud<TResultado>
{
    Task<TResultado> ManejarAsync(TSolicitud solicitud, CancellationToken ct);
}

/// <summary>Permiso que exige la solicitud. Toda solicitud debe declararlo o marcarse con <see cref="PublicaAttribute"/>.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class RequierePermisoAttribute(string permiso) : Attribute
{
    public string Permiso { get; } = permiso;
}

/// <summary>Solicitud que no requiere sesión (por ejemplo, iniciar sesión).</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class PublicaAttribute : Attribute;

public interface IMediador
{
    Task<TResultado> EnviarAsync<TResultado>(ISolicitud<TResultado> solicitud, CancellationToken ct = default);
}

/// <summary>
/// Único punto de entrada a los casos de uso: autoriza, valida y despacha.
/// La autorización vive aquí y no en los adaptadores, así ningún canal se la salta.
/// </summary>
public sealed class Mediador(IServiceProvider servicios, IUsuarioActual usuario) : IMediador
{
    private static readonly ConcurrentDictionary<Type, Func<Mediador, object, CancellationToken, object>> Despachos = new();

    public async Task<TResultado> EnviarAsync<TResultado>(ISolicitud<TResultado> solicitud, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        Autorizar(solicitud.GetType());

        var despacho = Despachos.GetOrAdd(solicitud.GetType(), CrearDespacho<TResultado>);
        return await (Task<TResultado>)despacho(this, solicitud, ct);
    }

    private void Autorizar(Type tipo)
    {
        if (tipo.GetCustomAttribute<PublicaAttribute>() is not null)
            return;

        var requerido = tipo.GetCustomAttribute<RequierePermisoAttribute>()
            ?? throw new InvalidOperationException($"{tipo.Name} no declara [RequierePermiso] ni [Publica].");

        if (!usuario.EstaAutenticado)
            throw new NoAutenticadoException();
        if (!usuario.TienePermiso(requerido.Permiso))
            throw new AccesoDenegadoException(requerido.Permiso);
    }

    private async Task<TResultado> Despachar<TSolicitud, TResultado>(TSolicitud solicitud, CancellationToken ct)
        where TSolicitud : ISolicitud<TResultado>
    {
        var validadores = servicios.GetServices<IValidator<TSolicitud>>().ToList();
        if (validadores.Count > 0)
        {
            var contexto = new ValidationContext<TSolicitud>(solicitud);
            var errores = new List<FluentValidation.Results.ValidationFailure>();
            foreach (var validador in validadores)
                errores.AddRange((await validador.ValidateAsync(contexto, ct)).Errors);
            if (errores.Count > 0)
                throw new ValidacionException(errores
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray()));
        }

        var manejador = servicios.GetRequiredService<IManejador<TSolicitud, TResultado>>();
        return await manejador.ManejarAsync(solicitud, ct);
    }

    private static Func<Mediador, object, CancellationToken, object> CrearDespacho<TResultado>(Type tipoSolicitud)
    {
        var metodo = typeof(Mediador)
            .GetMethod(nameof(Despachar), BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(tipoSolicitud, typeof(TResultado));
        return (mediador, solicitud, ct) => metodo.Invoke(mediador, [solicitud, ct])!;
    }
}
