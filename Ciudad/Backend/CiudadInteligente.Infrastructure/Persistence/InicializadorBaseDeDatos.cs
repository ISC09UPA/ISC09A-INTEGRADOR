namespace CiudadInteligente.Infrastructure.Persistence;

/// <summary>
/// Crea las tablas si no existen (al arrancar la API). No hace falta correr
/// ningun script a mano: el archivo .db aparece solo junto al proyecto API.
/// Si el archivo ya trae las tablas (de la version anterior) no se toca.
/// </summary>
public static class InicializadorBaseDeDatos
{
    public static void Inicializar(AppDbContext db) => db.Database.EnsureCreated();
}
