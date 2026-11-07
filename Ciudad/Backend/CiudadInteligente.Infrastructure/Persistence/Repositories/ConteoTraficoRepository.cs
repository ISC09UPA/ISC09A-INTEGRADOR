using System.Globalization;
using CiudadInteligente.Application.Interfaces;
using CiudadInteligente.Domain.Entities;
using Dapper;

namespace CiudadInteligente.Infrastructure.Persistence.Repositories;

/// <summary>Tabla ConteosTrafico con Dapper (ORM) sobre ADO.NET (Microsoft.Data.Sqlite).</summary>
public sealed class ConteoTraficoRepository(SqliteConnectionFactory fabrica) : IConteoTraficoRepository
{
    public async Task GuardarAsync(ConteoTrafico conteo)
    {
        const string sql = """
            INSERT INTO ConteosTrafico (RegistradoEn, Norte, Sur, Este, Oeste)
            VALUES (@RegistradoEn, @Norte, @Sur, @Este, @Oeste);
            """;

        using var conexion = fabrica.Crear();
        await conexion.ExecuteAsync(sql, new
        {
            RegistradoEn = conteo.RegistradoEn.ToString("O", CultureInfo.InvariantCulture),
            conteo.Norte,
            conteo.Sur,
            conteo.Este,
            conteo.Oeste
        });
    }

    public async Task<IReadOnlyList<ConteoTrafico>> ObtenerRecientesAsync(int cantidad)
    {
        const string sql = """
            SELECT Id, RegistradoEn, Norte, Sur, Este, Oeste
            FROM ConteosTrafico
            ORDER BY Id DESC
            LIMIT @Cantidad;
            """;

        using var conexion = fabrica.Crear();
        var filas = await conexion.QueryAsync<ConteoFila>(sql, new { Cantidad = cantidad });
        return filas.Select(f => new ConteoTrafico
        {
            Id = f.Id,
            RegistradoEn = DateTimeOffset.Parse(f.RegistradoEn, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            Norte = (int)f.Norte,
            Sur = (int)f.Sur,
            Este = (int)f.Este,
            Oeste = (int)f.Oeste
        }).ToList();
    }

    private sealed class ConteoFila
    {
        public long Id { get; set; }
        public string RegistradoEn { get; set; } = string.Empty;
        public long Norte { get; set; }
        public long Sur { get; set; }
        public long Este { get; set; }
        public long Oeste { get; set; }
    }
}
