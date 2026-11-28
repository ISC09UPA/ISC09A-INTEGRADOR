using AlmacenInteligente.Application.Abstracciones;
using AlmacenInteligente.Application.CasosDeUso;
using AlmacenInteligente.Application.Pdf;
using AlmacenInteligente.Application.Seguridad;
using FluentValidation;

namespace AlmacenInteligente.Application.Finanzas;

// Reporte mensual de movimientos financieros al estilo de Dolibarr (compta/bank/annuel.php):
// meses en filas y, por cada año, Debe (egresos: compras) y Haber (ingresos: ventas). Los reversos por
// cancelación restan en el mes en que se cancelaron.

public sealed record ValoresAnioDto(int Anio, decimal Debe, decimal Haber);

public sealed record FilaMesDto(int Mes, string Nombre, IReadOnlyList<ValoresAnioDto> PorAnio);

public sealed record ReporteMensualDto(
    IReadOnlyList<int> Anios, IReadOnlyList<FilaMesDto> Meses, IReadOnlyList<ValoresAnioDto> Totales, decimal SaldoActual);

/// <summary>Puerto de lectura: sumas por mes de los movimientos financieros y saldo histórico.</summary>
public interface IConsultasFinanzas
{
    Task<ReporteMensualDto> ReporteMensualAsync(int anioInicial, int anioFinal, CancellationToken ct);
}

/// <summary>Muestra <see cref="Anios"/> años terminando en <see cref="AnioFinal"/> (Dolibarr muestra tres).</summary>
[RequierePermiso(Permisos.Finanzas.Ver)]
public sealed record ReporteMensualFinanciero(int AnioFinal, int Anios = 3) : ISolicitud<ReporteMensualDto>;

[RequierePermiso(Permisos.Finanzas.Ver)]
public sealed record ReporteMensualFinancieroPdf(int AnioFinal, int Anios = 3) : ISolicitud<ArchivoDto>;

internal sealed class ReporteMensualValidador : AbstractValidator<ReporteMensualFinanciero>
{
    public ReporteMensualValidador()
    {
        RuleFor(x => x.AnioFinal).InclusiveBetween(2000, 2100).WithMessage("El año no es válido.");
        RuleFor(x => x.Anios).InclusiveBetween(1, 5).WithMessage("Se pueden comparar de 1 a 5 años.");
    }
}

internal sealed class ReporteMensualPdfValidador : AbstractValidator<ReporteMensualFinancieroPdf>
{
    public ReporteMensualPdfValidador()
    {
        RuleFor(x => x.AnioFinal).InclusiveBetween(2000, 2100).WithMessage("El año no es válido.");
        RuleFor(x => x.Anios).InclusiveBetween(1, 5).WithMessage("Se pueden comparar de 1 a 5 años.");
    }
}

internal sealed class ReporteMensualManejador(
        IConsultasFinanzas consultas, IGeneradorPdf pdf, IReloj reloj, IUsuarioActual usuario, Usuarios.IGestorUsuarios usuarios) :
    IManejador<ReporteMensualFinanciero, ReporteMensualDto>,
    IManejador<ReporteMensualFinancieroPdf, ArchivoDto>
{
    public Task<ReporteMensualDto> ManejarAsync(ReporteMensualFinanciero s, CancellationToken ct) =>
        consultas.ReporteMensualAsync(s.AnioFinal - s.Anios + 1, s.AnioFinal, ct);

    public async Task<ArchivoDto> ManejarAsync(ReporteMensualFinancieroPdf s, CancellationToken ct)
    {
        var reporte = await consultas.ReporteMensualAsync(s.AnioFinal - s.Anios + 1, s.AnioFinal, ct);
        var autor = usuario.Id is { } id && await usuarios.ObtenerAsync(id, ct) is { } u ? u.Nombre : "—";
        return new ArchivoDto($"reporte-mensual-{reporte.Anios[0]}-{s.AnioFinal}.pdf", "application/pdf",
            pdf.ReporteMensual(reporte, reloj.Ahora, autor));
    }
}
