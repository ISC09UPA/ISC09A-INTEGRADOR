using System.Globalization;
using AlmacenInteligente.Application.Finanzas;
using AlmacenInteligente.Domain.Finanzas;
using Microsoft.EntityFrameworkCore;

namespace AlmacenInteligente.Infrastructure.Persistencia;

internal sealed class ConsultasFinanzasEf(AlmacenDbContext db) : IConsultasFinanzas
{
    private static readonly CultureInfo Mx = CultureInfo.GetCultureInfo("es-MX");
    private static readonly TimeZoneInfo ZonaMexico = TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");

    public async Task<ReporteMensualDto> ReporteMensualAsync(int anioInicial, int anioFinal, CancellationToken ct)
    {
        var inicio = InicioDia(new DateOnly(anioInicial, 1, 1));
        var fin = InicioDia(new DateOnly(anioFinal + 1, 1, 1));

        // Importe es un value object con conversión: se suma en memoria (un registro por documento).
        var movimientos = await db.MovimientosFinancieros.AsNoTracking()
            .Where(m => m.Registro.Fecha >= inicio && m.Registro.Fecha < fin)
            .Select(m => new { m.Registro.Fecha, m.Naturaleza, m.Importe })
            .ToListAsync(ct);

        var sumas = movimientos
            .Select(m => (Local: TimeZoneInfo.ConvertTime(m.Fecha, ZonaMexico), m.Naturaleza, Total: m.Importe.Total.Monto))
            .GroupBy(m => (m.Local.Year, m.Local.Month))
            .ToDictionary(g => g.Key, g => (
                Debe: g.Where(x => x.Naturaleza == NaturalezaFinanciera.Egreso).Sum(x => x.Total),
                Haber: g.Where(x => x.Naturaleza == NaturalezaFinanciera.Ingreso).Sum(x => x.Total)));

        var anios = Enumerable.Range(anioInicial, anioFinal - anioInicial + 1).ToList();
        var meses = Enumerable.Range(1, 12).Select(mes => new FilaMesDto(mes, Mx.DateTimeFormat.GetMonthName(mes),
            anios.Select(a => sumas.TryGetValue((a, mes), out var s) ? new ValoresAnioDto(a, s.Debe, s.Haber) : new ValoresAnioDto(a, 0, 0)).ToList()))
            .ToList();
        var totales = anios.Select(a => new ValoresAnioDto(a,
            meses.Sum(m => m.PorAnio.First(v => v.Anio == a).Debe),
            meses.Sum(m => m.PorAnio.First(v => v.Anio == a).Haber))).ToList();

        // Saldo actual: todo lo cobrado menos todo lo pagado desde el inicio, no solo los años mostrados.
        var historico = await db.MovimientosFinancieros.AsNoTracking().Select(m => new { m.Naturaleza, m.Importe }).ToListAsync(ct);
        var saldo = historico.Sum(m => m.Naturaleza == NaturalezaFinanciera.Ingreso ? m.Importe.Total.Monto : -m.Importe.Total.Monto);

        return new ReporteMensualDto(anios, meses, totales, saldo);
    }

    private static DateTimeOffset InicioDia(DateOnly fecha)
    {
        var local = fecha.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, ZonaMexico.GetUtcOffset(local)).ToUniversalTime();
    }
}
