namespace CiudadInteligente.Domain.Enums;

/// <summary>
/// Quien usa la app:
///   Administrador: todo, incluido el control manual del cruce y los usuarios.
///   Operador:      monitorea el cruce y las estadisticas, y atiende emergencias.
///
/// Ciudadano (usuario normal) NO es una cuenta: es cualquier persona que abre
/// la app sin iniciar sesion. Por eso no esta en este enum ni en la base de datos;
/// solo puede ver el estado del cruce y el trafico (api/publico/...).
/// </summary>
public enum RolUsuario
{
    Administrador,
    Operador
}

/// <summary>Los roles como texto (asi viajan en el token y en la API).</summary>
public static class RolesUsuario
{
    public const string Administrador = "administrador";
    public const string Operador = "operador";

    /// <summary>
    /// Usuario normal: sin cuenta ni contrasena. Solo lectura de api/publico/*.
    /// Es el rol que la app asume cuando no hay sesion iniciada.
    /// </summary>
    public const string Ciudadano = "ciudadano";

    /// <summary>No es una persona: la camara o el simulador, con la llave X-Api-Key.</summary>
    public const string Dispositivo = "dispositivo";

    public static string Texto(RolUsuario rol) => rol switch
    {
        RolUsuario.Administrador => Administrador,
        _ => Operador
    };

    public static bool TryParse(string? texto, out RolUsuario rol)
    {
        switch (texto?.Trim().ToLowerInvariant())
        {
            case Administrador:
                rol = RolUsuario.Administrador;
                return true;
            case Operador:
                rol = RolUsuario.Operador;
                return true;
            default:
                rol = RolUsuario.Operador;
                return false;
        }
    }
}
