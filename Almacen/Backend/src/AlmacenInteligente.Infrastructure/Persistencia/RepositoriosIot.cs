using AlmacenInteligente.Application.Iot;
using AlmacenInteligente.Application.Terceros;
using AlmacenInteligente.Domain.Iot;
using Microsoft.EntityFrameworkCore;

namespace AlmacenInteligente.Infrastructure.Persistencia;

internal sealed class FotosRostroRepositorio(AlmacenDbContext db) : IFotosRostroRepositorio
{
    public Task<FotoRostro?> ObtenerAsync(Guid id, CancellationToken ct = default) =>
        db.FotosRostro.FirstOrDefaultAsync(f => f.Id == id, ct);

    public async Task<IReadOnlyList<FotoRostro>> ListarAsync(Guid usuarioId, CancellationToken ct = default) =>
        await db.FotosRostro.Where(f => f.UsuarioId == usuarioId).OrderBy(f => f.SubidaEn).ToListAsync(ct);

    public async Task<IReadOnlyList<FotoRostro>> ListarTodasAsync(CancellationToken ct = default) =>
        await db.FotosRostro.OrderBy(f => f.UsuarioId).ThenBy(f => f.SubidaEn).ToListAsync(ct);

    public void Agregar(FotoRostro foto) => db.FotosRostro.Add(foto);

    public void Eliminar(FotoRostro foto) => db.FotosRostro.Remove(foto);
}

internal sealed class AccesosIotRepositorio(AlmacenDbContext db) : IAccesosIotRepositorio
{
    private static readonly TimeZoneInfo ZonaMexico = TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");

    public void Agregar(AccesoIot acceso) => db.AccesosIot.Add(acceso);

    public async Task<Pagina<AccesoIotDto>> ListarAsync(FiltroAccesosIot f, CancellationToken ct = default)
    {
        var q = db.AccesosIot.AsNoTracking();
        if (f.Permitido is { } permitido)
            q = q.Where(a => a.Permitido == permitido);
        if (f.Desde is { } desde)
        {
            var inicio = InicioDelDia(desde);
            q = q.Where(a => a.Fecha >= inicio);
        }
        if (f.Hasta is { } hasta)
        {
            var fin = InicioDelDia(hasta.AddDays(1));
            q = q.Where(a => a.Fecha < fin);
        }

        var total = await q.CountAsync(ct);
        var filas = await (
            from a in q.OrderByDescending(a => a.Fecha).Skip((f.NumeroPagina - 1) * f.Tamano).Take(f.Tamano)
            join u in db.Users on a.UsuarioId equals u.Id into usuarios
            from u in usuarios.DefaultIfEmpty()
            select new { a, Nombre = u == null ? null : u.Nombre }
        ).ToListAsync(ct);

        var elementos = filas
            .Select(x => new AccesoIotDto(x.a.Id, TimeZoneInfo.ConvertTime(x.a.Fecha, ZonaMexico), x.a.Nfc, x.a.UsuarioId, x.Nombre,
                x.a.Permitido, x.a.Confianza, x.a.Motivo))
            .ToList();
        return new Pagina<AccesoIotDto>(elementos, total, f.NumeroPagina, f.Tamano);
    }

    private static DateTimeOffset InicioDelDia(DateOnly dia)
    {
        var local = dia.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, ZonaMexico.GetUtcOffset(local)).ToUniversalTime();
    }
}

/// <summary>
/// El candado vive en una fila única de iot_sesion_venta. Cada operación es una sola sentencia SQL, así que
/// PostgreSQL la serializa: si dos agentes lo piden a la vez, el segundo ve la fila ya tomada y no la cambia.
/// Las condiciones son las mismas de <see cref="SesionVentaInteligente"/>.
/// </summary>
internal sealed class CandadoVentaInteligente(AlmacenDbContext db) : ICandadoVentaInteligente
{
    private const int Id = SesionVentaInteligente.IdUnico;

    public Task<SesionVentaInteligente?> ObtenerAsync(CancellationToken ct = default) =>
        db.SesionVentaInteligente.AsNoTracking().FirstOrDefaultAsync(s => s.Id == Id, ct);

    public async Task<bool> IntentarTomarAsync(Guid usuarioId, Guid almacenId, DateTimeOffset ahora, DateTimeOffset venceAntesDe, CancellationToken ct = default)
    {
        // Npgsql solo acepta DateTimeOffset en UTC para timestamptz.
        var (momento, limite, nueva) = (ahora.ToUniversalTime(), venceAntesDe.ToUniversalTime(), Guid.CreateVersion7());
        var filas = await db.Database.ExecuteSqlAsync($"""
            INSERT INTO iot_sesion_venta AS s (id, sesion_id, usuario_id, almacen_id, inicio, ultimo_latido)
            VALUES ({Id}, {nueva}, {usuarioId}, {almacenId}, {momento}, {momento})
            ON CONFLICT (id) DO UPDATE SET
                sesion_id = CASE WHEN s.usuario_id = {usuarioId} AND s.ultimo_latido >= {limite} THEN s.sesion_id ELSE EXCLUDED.sesion_id END,
                inicio = CASE WHEN s.usuario_id = {usuarioId} AND s.ultimo_latido >= {limite} THEN s.inicio ELSE EXCLUDED.inicio END,
                usuario_id = EXCLUDED.usuario_id,
                almacen_id = EXCLUDED.almacen_id,
                ultimo_latido = EXCLUDED.ultimo_latido
            WHERE s.usuario_id IS NULL OR s.usuario_id = {usuarioId} OR s.ultimo_latido < {limite}
            """, ct);
        return filas == 1;
    }

    public async Task<bool> LatidoAsync(Guid usuarioId, DateTimeOffset ahora, CancellationToken ct = default)
    {
        var momento = ahora.ToUniversalTime();
        return await db.Database.ExecuteSqlAsync(
            $"UPDATE iot_sesion_venta SET ultimo_latido = {momento} WHERE id = {Id} AND usuario_id = {usuarioId}", ct) == 1;
    }

    public async Task LiberarAsync(Guid usuarioId, CancellationToken ct = default)
    {
        // Los escaneos que nadie alcanzó a tomar ya no pertenecen a ninguna venta.
        await db.Database.ExecuteSqlAsync($"""
            DELETE FROM iot_escaneos
            WHERE NOT procesado AND sesion_id = (SELECT sesion_id FROM iot_sesion_venta WHERE id = {Id} AND usuario_id = {usuarioId})
            """, ct);
        await db.Database.ExecuteSqlAsync($"""
            UPDATE iot_sesion_venta SET sesion_id = NULL, usuario_id = NULL, almacen_id = NULL, inicio = NULL, ultimo_latido = NULL
            WHERE id = {Id} AND usuario_id = {usuarioId}
            """, ct);
    }

    public async Task<bool> RegistrarEscaneoAsync(Guid productoId, DateTimeOffset ahora, DateTimeOffset venceAntesDe, CancellationToken ct = default)
    {
        var (momento, limite, id) = (ahora.ToUniversalTime(), venceAntesDe.ToUniversalTime(), Guid.CreateVersion7());
        return await db.Database.ExecuteSqlAsync($"""
            INSERT INTO iot_escaneos (id, sesion_id, producto_id, fecha, procesado)
            SELECT {id}, s.sesion_id, {productoId}, {momento}, FALSE
            FROM iot_sesion_venta s
            WHERE s.id = {Id} AND s.usuario_id IS NOT NULL AND s.ultimo_latido >= {limite}
            """, ct) == 1;
    }

    public async Task<IReadOnlyList<EscaneoIot>> TomarEscaneosPendientesAsync(Guid usuarioId, CancellationToken ct = default)
    {
        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        var escaneos = await db.EscaneosIot.FromSql($"""
            SELECT e.* FROM iot_escaneos e
            JOIN iot_sesion_venta s ON s.sesion_id = e.sesion_id
            WHERE s.id = {Id} AND s.usuario_id = {usuarioId} AND NOT e.procesado
            ORDER BY e.fecha
            FOR UPDATE OF e SKIP LOCKED
            """).ToListAsync(ct);
        foreach (var e in escaneos)
            e.MarcarProcesado();
        await db.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);
        return escaneos;
    }
}
