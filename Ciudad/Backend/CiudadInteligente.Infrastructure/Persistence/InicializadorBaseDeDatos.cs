namespace CiudadInteligente.Infrastructure.Persistence;

/// <summary>
/// Crea las tablas si no existen (al arrancar la API) en la base configurada
/// en ConnectionStrings:CiudadInteligente (Azure SQL). La base de datos ya debe
/// existir en el servidor; si ya trae las tablas no se toca.
/// </summary>
public static class InicializadorBaseDeDatos
{
    public static void Inicializar(AppDbContext db) => db.Database.EnsureCreated();
}
