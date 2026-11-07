using System.Globalization;
using CiudadInteligente.Application.Interfaces;
using CiudadInteligente.Domain.Entities;
using CiudadInteligente.Domain.Enums;
using Dapper;

namespace CiudadInteligente.Infrastructure.Persistence.Repositories;

/// <summary>Tabla Emergencias con Dapper (ORM) sobre ADO.NET (Microsoft.Data.Sqlite).</summary>
public sealed class EmergenciaRepository(SqliteConnectionFactory fabrica) : IEmergenciaRepository
{
    public async Task<int> RegistrarAsync(Emergencia emergencia)
    {
        const string sql = """
            INSERT INTO Emergencias (Unidad, Acceso, Tipo, EtaMinutos, SolicitadaEn, FinalizadaEn)
            VALUES (@Unidad, @Acceso, @Tipo, @EtaMinutos, @SolicitadaEn, NULL);
            SELECT last_insert_rowid();
            """;

        using var conexion = fabrica.Crear();
        var id = await conexion.ExecuteScalarAsync<long>(sql, new
        {
            emergencia.Unidad,
            emergencia.Acceso,
            Tipo = emergencia.Tipo.ToString(),
            emergencia.EtaMinutos,
            SolicitadaEn = FechaATexto(emergencia.SolicitadaEn)
        });

        return (int)id;
    }

    public async Task<int> FinalizarActivasAsync(DateTimeOffset finalizadaEn)
    {
        const string sql = "UPDATE Emergencias SET FinalizadaEn = @FinalizadaEn WHERE FinalizadaEn IS NULL;";

        using var conexion = fabrica.Crear();
        return await conexion.ExecuteAsync(sql, new { FinalizadaEn = FechaATexto(finalizadaEn) });
    }

    public async Task<IReadOnlyList<Emergencia>> ObtenerRecientesAsync(int cantidad)
    {
        const string sql = """
            SELECT Id, Unidad, Acceso, Tipo, EtaMinutos, SolicitadaEn, FinalizadaEn
            FROM Emergencias
            ORDER BY Id DESC
            LIMIT @Cantidad;
            """;

        using var conexion = fabrica.Crear();
        var filas = await conexion.QueryAsync<EmergenciaFila>(sql, new { Cantidad = cantidad });
        return filas.Select(f => f.AEntidad()).ToList();
    }

    private static string FechaATexto(DateTimeOffset fecha) => fecha.ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset TextoAFecha(string texto) =>
        DateTimeOffset.Parse(texto, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    /// <summary>
    /// Fila tal como sale de SQLite (INTEGER = long, fechas = texto). Dapper
    /// la llena por nombre de columna y luego se convierte a la entidad.
    /// </summary>
    private sealed class EmergenciaFila
    {
        public long Id { get; set; }
        public string Unidad { get; set; } = string.Empty;
        public long Acceso { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public long EtaMinutos { get; set; }
        public string SolicitadaEn { get; set; } = string.Empty;
        public string? FinalizadaEn { get; set; }

        public Emergencia AEntidad() => new()
        {
            Id = (int)Id,
            Unidad = Unidad,
            Acceso = (int)Acceso,
            Tipo = Enum.TryParse<TipoEmergencia>(Tipo, ignoreCase: true, out var t) ? t : TipoEmergencia.Medica,
            EtaMinutos = (int)EtaMinutos,
            SolicitadaEn = TextoAFecha(SolicitadaEn),
            FinalizadaEn = FinalizadaEn is null ? null : TextoAFecha(FinalizadaEn)
        };
    }
}
