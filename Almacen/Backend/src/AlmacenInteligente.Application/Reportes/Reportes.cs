using AlmacenInteligente.Application.CasosDeUso;
using AlmacenInteligente.Application.Consultas;
using AlmacenInteligente.Application.Seguridad;
using FluentValidation;

namespace AlmacenInteligente.Application.Reportes;

[RequierePermiso(Permisos.Finanzas.Ver)]
public sealed record ListarMovimientosFinancieros(FiltroFinanzas Filtro) : ISolicitud<ResumenFinancieroDto>;

[RequierePermiso(Permisos.Reportes.Ver)]
public sealed record ReporteVentas(DateOnly Desde, DateOnly Hasta) : ISolicitud<ReporteVentasDto>;

[RequierePermiso(Permisos.Reportes.Ver)]
public sealed record ReporteCompras(DateOnly Desde, DateOnly Hasta) : ISolicitud<ReporteComprasDto>;

[RequierePermiso(Permisos.Reportes.Ver)]
public sealed record ReporteFinanciero(DateOnly Desde, DateOnly Hasta) : ISolicitud<ReporteFinancieroDto>;

[RequierePermiso(Permisos.Reportes.Ver)]
public sealed record ReporteInventario : ISolicitud<ReporteInventarioDto>;

[RequierePermiso(Permisos.Reportes.Ver)]
public sealed record ReporteMargen(DateOnly Desde, DateOnly Hasta) : ISolicitud<IReadOnlyList<MargenProductoDto>>;

/// <summary>Rango válido y de máximo 3 años, para no recorrer toda la historia por error.</summary>
internal static class RangoFechas
{
    public static void Validar<T>(AbstractValidator<T> v, Func<T, DateOnly> desde, Func<T, DateOnly> hasta) =>
        v.RuleFor(x => x).Must(x => desde(x) <= hasta(x) && hasta(x).DayNumber - desde(x).DayNumber <= 366 * 3)
            .WithName("Periodo").WithMessage("El periodo no es válido (la fecha inicial va antes que la final, máximo 3 años).");
}

internal sealed class ReporteVentasValidador : AbstractValidator<ReporteVentas>
{
    public ReporteVentasValidador() => RangoFechas.Validar(this, x => x.Desde, x => x.Hasta);
}

internal sealed class ReporteComprasValidador : AbstractValidator<ReporteCompras>
{
    public ReporteComprasValidador() => RangoFechas.Validar(this, x => x.Desde, x => x.Hasta);
}

internal sealed class ReporteFinancieroValidador : AbstractValidator<ReporteFinanciero>
{
    public ReporteFinancieroValidador() => RangoFechas.Validar(this, x => x.Desde, x => x.Hasta);
}

internal sealed class ReporteMargenValidador : AbstractValidator<ReporteMargen>
{
    public ReporteMargenValidador() => RangoFechas.Validar(this, x => x.Desde, x => x.Hasta);
}

internal sealed class ReportesManejador(IConsultas consultas) :
    IManejador<ListarMovimientosFinancieros, ResumenFinancieroDto>,
    IManejador<ReporteVentas, ReporteVentasDto>,
    IManejador<ReporteCompras, ReporteComprasDto>,
    IManejador<ReporteFinanciero, ReporteFinancieroDto>,
    IManejador<ReporteInventario, ReporteInventarioDto>,
    IManejador<ReporteMargen, IReadOnlyList<MargenProductoDto>>
{
    public Task<ResumenFinancieroDto> ManejarAsync(ListarMovimientosFinancieros s, CancellationToken ct) =>
        consultas.ListarMovimientosFinancierosAsync(s.Filtro, ct);

    public Task<ReporteVentasDto> ManejarAsync(ReporteVentas s, CancellationToken ct) => consultas.ReporteVentasAsync(s.Desde, s.Hasta, ct);

    public Task<ReporteComprasDto> ManejarAsync(ReporteCompras s, CancellationToken ct) => consultas.ReporteComprasAsync(s.Desde, s.Hasta, ct);

    public Task<ReporteFinancieroDto> ManejarAsync(ReporteFinanciero s, CancellationToken ct) => consultas.ReporteFinancieroAsync(s.Desde, s.Hasta, ct);

    public Task<ReporteInventarioDto> ManejarAsync(ReporteInventario s, CancellationToken ct) => consultas.ReporteInventarioAsync(ct);

    public Task<IReadOnlyList<MargenProductoDto>> ManejarAsync(ReporteMargen s, CancellationToken ct) => consultas.ReporteMargenAsync(s.Desde, s.Hasta, ct);
}
