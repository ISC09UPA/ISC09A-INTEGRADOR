using System.Collections.Concurrent;
using System.Reflection;
using AlmacenInteligente.Application.Notificaciones;
using AlmacenInteligente.Domain.Common;
using AlmacenInteligente.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AlmacenInteligente.Infrastructure.Mensajeria;

/// <summary>
/// Entrega los eventos del outbox a sus <see cref="IManejadorEvento{TEvento}"/> (hoy, los correos).
/// Corre en la API y en la web a la vez: cada lote se toma con FOR UPDATE SKIP LOCKED, así un mensaje lo procesa
/// una sola instancia. Si un manejador falla se reintenta con espera creciente (30 s, 1 min, 2 min…) hasta
/// <see cref="MaxIntentos"/>; el error queda en la columna ultimo_error.
/// </summary>
internal sealed class ProcesadorOutbox(IServiceScopeFactory scopes, IConfiguration configuracion, ILogger<ProcesadorOutbox> log)
    : BackgroundService
{
    public const int MaxIntentos = 8;
    private const int Lote = 20;

    private static readonly ConcurrentDictionary<Type, Func<IServiceProvider, IDomainEvent, CancellationToken, Task>> Despachos = new();

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var intervalo = TimeSpan.FromSeconds(Math.Max(1, configuracion.GetValue("Outbox:IntervaloSegundos", 10)));
        using var reloj = new PeriodicTimer(intervalo);
        while (await reloj.WaitForNextTickAsync(ct))
        {
            try
            {
                while (await ProcesarLoteAsync(ct) == Lote) { }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                // P. ej. la base no responde o la tabla aún no existe porque la web no ha aplicado las migraciones.
                log.LogWarning(ex, "No se pudo leer el outbox; se reintenta en {Intervalo}.", intervalo);
            }
        }
    }

    private async Task<int> ProcesarLoteAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AlmacenDbContext>();
        var ahora = DateTimeOffset.UtcNow;

        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        var mensajes = await db.Outbox.FromSql($"""
            SELECT * FROM outbox
            WHERE procesado_en IS NULL AND intentos < {MaxIntentos}
              AND (proximo_intento_en IS NULL OR proximo_intento_en <= {ahora})
            ORDER BY ocurrio_en
            LIMIT {Lote}
            FOR UPDATE SKIP LOCKED
            """).ToListAsync(ct);

        foreach (var mensaje in mensajes)
        {
            try
            {
                var evento = mensaje.LeerEvento() ?? throw new InvalidOperationException($"Tipo de evento desconocido: {mensaje.Tipo}.");
                await Despachos.GetOrAdd(evento.GetType(), CrearDespacho)(scope.ServiceProvider, evento, ct);
                mensaje.ProcesadoEn = DateTimeOffset.UtcNow;
                mensaje.UltimoError = null;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                mensaje.Intentos++;
                mensaje.UltimoError = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
                mensaje.ProximoIntentoEn = DateTimeOffset.UtcNow.AddSeconds(30 * Math.Pow(2, mensaje.Intentos - 1));
                if (mensaje.Intentos >= MaxIntentos)
                    log.LogError(ex, "El evento {Tipo} {Id} falló {Intentos} veces y ya no se reintentará.", mensaje.Tipo, mensaje.Id, mensaje.Intentos);
                else
                    log.LogWarning(ex, "Falló el evento {Tipo} {Id} (intento {Intentos}); se reintenta a las {Proximo:t}.",
                        mensaje.Tipo, mensaje.Id, mensaje.Intentos, mensaje.ProximoIntentoEn);
            }
        }

        await db.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);
        return mensajes.Count;
    }

    private static Func<IServiceProvider, IDomainEvent, CancellationToken, Task> CrearDespacho(Type tipo)
    {
        var metodo = typeof(ProcesadorOutbox).GetMethod(nameof(Despachar), BindingFlags.NonPublic | BindingFlags.Static)!.MakeGenericMethod(tipo);
        return (servicios, evento, ct) => (Task)metodo.Invoke(null, [servicios, evento, ct])!;
    }

    /// <summary>Los eventos sin manejador (p. ej. RecepcionConfirmada) se marcan como procesados sin hacer nada.</summary>
    private static async Task Despachar<TEvento>(IServiceProvider servicios, IDomainEvent evento, CancellationToken ct)
        where TEvento : IDomainEvent
    {
        foreach (var manejador in servicios.GetServices<IManejadorEvento<TEvento>>())
            await manejador.ManejarAsync((TEvento)evento, ct);
    }
}
