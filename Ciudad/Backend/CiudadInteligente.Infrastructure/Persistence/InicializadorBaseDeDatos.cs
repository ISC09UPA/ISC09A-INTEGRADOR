using Dapper;

namespace CiudadInteligente.Infrastructure.Persistence;

/// <summary>
/// Crea las tablas si no existen (al arrancar la API). No hace falta correr
/// ningun script a mano: el archivo .db aparece solo junto al proyecto API.
/// Las fechas se guardan como texto ISO 8601 (formato "O"), que SQLite
/// ordena bien alfabeticamente.
/// </summary>
public static class InicializadorBaseDeDatos
{
    private const string Esquema = """
        CREATE TABLE IF NOT EXISTS Emergencias (
            Id            INTEGER PRIMARY KEY AUTOINCREMENT,
            Unidad        TEXT    NOT NULL,
            Acceso        INTEGER NOT NULL CHECK (Acceso BETWEEN 1 AND 4),
            Tipo          TEXT    NOT NULL,
            EtaMinutos    INTEGER NOT NULL,
            SolicitadaEn  TEXT    NOT NULL,
            FinalizadaEn  TEXT    NULL
        );

        CREATE TABLE IF NOT EXISTS ConteosTrafico (
            Id            INTEGER PRIMARY KEY AUTOINCREMENT,
            RegistradoEn  TEXT    NOT NULL,
            Norte         INTEGER NOT NULL,
            Sur           INTEGER NOT NULL,
            Este          INTEGER NOT NULL,
            Oeste         INTEGER NOT NULL
        );

        CREATE INDEX IF NOT EXISTS IX_ConteosTrafico_RegistradoEn ON ConteosTrafico (RegistradoEn);
        """;

    public static void Inicializar(SqliteConnectionFactory fabrica)
    {
        using var conexion = fabrica.Crear();
        conexion.Execute(Esquema);
    }
}
