using AlmacenInteligente.Application.Abstracciones;
using AlmacenInteligente.Application.CasosDeUso;
using AlmacenInteligente.Application.Iot;
using AlmacenInteligente.Application.Usuarios;
using AlmacenInteligente.Domain.Common;
using AlmacenInteligente.Domain.Iot;

namespace AlmacenInteligente.Application.Tests;

public class InicioSesionIotTests
{
    private readonly RelojFalso _reloj = new();
    private readonly SolicitudesEnMemoria _solicitudes = new();
    private readonly EmisorFalso _emisor = new();
    private readonly ServicioInicioSesionIot _servicio;

    public InicioSesionIotTests() => _servicio = new ServicioInicioSesionIot(_solicitudes, _emisor, _reloj);

    [Fact]
    public async Task Solo_puede_haber_una_solicitud_pendiente_vigente()
    {
        await _servicio.CrearAsync();

        var ex = await Assert.ThrowsAsync<DomainException>(() => _servicio.CrearAsync());
        Assert.Equal(ServicioInicioSesionIot.MensajeOcupado, ex.Message);

        _reloj.Avanzar(TimeSpan.FromSeconds(61));
        await _servicio.CrearAsync();
    }

    [Fact]
    public async Task Al_cancelar_se_libera_el_lector()
    {
        var s = await _servicio.CrearAsync();
        await _servicio.CancelarAsync(s.Id);

        Assert.Equal("cancelada", (await _servicio.ConsultarAsync(s.Id)).Estado);
        await _servicio.CrearAsync();
    }

    [Fact]
    public async Task Pendiente_y_luego_expirada_sin_acceso()
    {
        var s = await _servicio.CrearAsync();
        Assert.Equal("pendiente", (await _servicio.ConsultarAsync(s.Id)).Estado);

        _reloj.Avanzar(TimeSpan.FromSeconds(61));
        var estado = await _servicio.ConsultarAsync(s.Id);
        Assert.Equal("expirada", estado.Estado);
        Assert.Null(estado.Token);
    }

    [Fact]
    public async Task Acceso_permitido_entrega_el_token_una_sola_vez()
    {
        var s = await _servicio.CrearAsync();
        var usuario = Guid.NewGuid();
        _reloj.Avanzar(TimeSpan.FromSeconds(5));
        await _solicitudes.ResolverConAccesoAsync(true, usuario, "Acceso permitido", _reloj.Ahora);

        var primero = await _servicio.ConsultarAsync(s.Id);
        Assert.Equal("completada", primero.Estado);
        Assert.Equal($"token-{usuario}", primero.Token!.Token);

        var segundo = await _servicio.ConsultarAsync(s.Id);
        Assert.Equal("consumida", segundo.Estado);
        Assert.Null(segundo.Token);
    }

    [Fact]
    public async Task Acceso_negado_cierra_la_solicitud_con_el_motivo()
    {
        var s = await _servicio.CrearAsync();
        _reloj.Avanzar(TimeSpan.FromSeconds(3));
        await _solicitudes.ResolverConAccesoAsync(false, Guid.NewGuid(), "Rostro no coincide", _reloj.Ahora);

        var estado = await _servicio.ConsultarAsync(s.Id);
        Assert.Equal("negada", estado.Estado);
        Assert.Equal("Rostro no coincide", estado.Motivo);
        // Ya no bloquea: la app puede reintentar.
        await _servicio.CrearAsync();
    }

    [Fact]
    public async Task Acceso_anterior_a_la_solicitud_no_la_resuelve()
    {
        var antes = _reloj.Ahora;
        _reloj.Avanzar(TimeSpan.FromSeconds(1));
        var s = await _servicio.CrearAsync();
        await _solicitudes.ResolverConAccesoAsync(true, Guid.NewGuid(), "Acceso permitido", antes);

        Assert.Equal("pendiente", (await _servicio.ConsultarAsync(s.Id)).Estado);
    }

    [Fact]
    public async Task Usuario_desactivado_despues_del_acceso_se_reporta_negado()
    {
        var s = await _servicio.CrearAsync();
        await _solicitudes.ResolverConAccesoAsync(true, Guid.NewGuid(), "Acceso permitido", _reloj.Ahora);
        _emisor.Falla = true;

        var estado = await _servicio.ConsultarAsync(s.Id);
        Assert.Equal("negada", estado.Estado);
        Assert.Null(estado.Token);
    }

    [Fact]
    public async Task Solicitud_inexistente_es_no_encontrada()
    {
        await Assert.ThrowsAsync<NoEncontradoException>(() => _servicio.ConsultarAsync(Guid.NewGuid()));
    }

    private sealed class RelojFalso : IReloj
    {
        public DateTimeOffset Ahora { get; private set; } = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);
        public DateOnly Hoy => DateOnly.FromDateTime(Ahora.UtcDateTime);
        public void Avanzar(TimeSpan tiempo) => Ahora += tiempo;
    }

    private sealed class EmisorFalso : IEmisorTokens
    {
        public bool Falla { get; set; }

        public Task<TokenApiDto> EmitirAsync(Guid usuarioId, CancellationToken ct = default) => Falla
            ? throw new DomainException("El usuario no existe o está desactivado.")
            : Task.FromResult(new TokenApiDto($"token-{usuarioId}", DateTimeOffset.UtcNow.AddHours(8), "Ana", [], []));
    }

    /// <summary>Las mismas reglas que las sentencias SQL del repositorio real, aplicadas con la entidad de dominio.</summary>
    private sealed class SolicitudesEnMemoria : ISolicitudesInicioSesionIot
    {
        private readonly List<SolicitudInicioSesionIot> _filas = [];

        public Task<bool> IntentarCrearAsync(SolicitudInicioSesionIot solicitud, CancellationToken ct = default)
        {
            if (_filas.Any(f => f.EstaPendiente(solicitud.Creada)))
                return Task.FromResult(false);
            _filas.Add(solicitud);
            return Task.FromResult(true);
        }

        public Task<SolicitudInicioSesionIot?> ObtenerAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_filas.FirstOrDefault(f => f.Id == id));

        public Task ResolverConAccesoAsync(bool permitido, Guid? usuarioId, string motivo, DateTimeOffset fechaAcceso, CancellationToken ct = default)
        {
            var pendiente = _filas.FirstOrDefault(f => f.AceptaAcceso(fechaAcceso));
            if (pendiente is not null)
            {
                if (permitido && usuarioId is { } u)
                    pendiente.Completar(u, fechaAcceso);
                else
                    pendiente.Negar(motivo, fechaAcceso);
            }
            return Task.CompletedTask;
        }

        public Task<Guid?> ConsumirAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_filas.FirstOrDefault(f => f.Id == id)?.Consumir());

        public Task CancelarAsync(Guid id, CancellationToken ct = default)
        {
            _filas.FirstOrDefault(f => f.Id == id)?.Cancelar();
            return Task.CompletedTask;
        }
    }
}
