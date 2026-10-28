using System.Reflection;

namespace AlmacenInteligente.Application.Seguridad;

/// <summary>
/// Permisos atómicos. Cada caso de uso exige uno; los roles son conjuntos de permisos (ver <see cref="MatrizPermisos"/>),
/// así la matriz se ajusta sin tocar los casos de uso.
/// </summary>
public static class Permisos
{
    public static class Usuarios
    {
        public const string Gestionar = "usuarios.gestionar";
    }

    public static class Clientes
    {
        public const string Ver = "clientes.ver";
        public const string Crear = "clientes.crear";
        public const string Editar = "clientes.editar";
    }

    public static class Proveedores
    {
        public const string Ver = "proveedores.ver";
        public const string Crear = "proveedores.crear";
        public const string Editar = "proveedores.editar";
    }

    public static class Productos
    {
        public const string Ver = "productos.ver";
        public const string Crear = "productos.crear";
        public const string Editar = "productos.editar";
        public const string Aprobar = "productos.aprobar";
    }

    public static class Almacenes
    {
        public const string Ver = "almacenes.ver";
        public const string Gestionar = "almacenes.gestionar";
        public const string VerExistencias = "existencias.ver";
    }

    public static class Recepciones
    {
        public const string Ver = "recepciones.ver";
        public const string Crear = "recepciones.crear";
        public const string Cancelar = "recepciones.cancelar";
    }

    public static class Ventas
    {
        public const string Ver = "ventas.ver";
        public const string Crear = "ventas.crear";
        public const string Cancelar = "ventas.cancelar";
    }

    public static class Traspasos
    {
        public const string Ver = "traspasos.ver";
        public const string Crear = "traspasos.crear";
    }

    public static class Finanzas
    {
        public const string Ver = "finanzas.ver";
    }

    public static class Reportes
    {
        public const string Ver = "reportes.ver";
    }

    public static class Api
    {
        /// <summary>Ver el apartado "API web" en Blazor y generar un token para usar la API.</summary>
        public const string Acceder = "api.acceder";
    }

    public static class Alertas
    {
        /// <summary>Recibe el correo de bajo stock.</summary>
        public const string RecibirBajoStock = "alertas.bajostock";
    }

    /// <summary>Todos los permisos declarados en esta clase.</summary>
    public static IReadOnlyList<string> Todos { get; } = typeof(Permisos)
        .GetNestedTypes()
        .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static))
        .Where(f => f.IsLiteral && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToList();
}
