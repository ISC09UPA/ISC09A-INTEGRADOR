using System.Data;
using Microsoft.Data.Sqlite;

namespace CiudadInteligente.Infrastructure.Persistence;

/// <summary>
/// Crea conexiones ADO.NET (SqliteConnection) para que Dapper las use.
/// Una conexion nueva por operacion: SQLite es un archivo local y abrirla
/// es barato; asi no se comparte una conexion entre hilos.
/// </summary>
public sealed class SqliteConnectionFactory(string cadenaConexion)
{
    public IDbConnection Crear() => new SqliteConnection(cadenaConexion);
}
