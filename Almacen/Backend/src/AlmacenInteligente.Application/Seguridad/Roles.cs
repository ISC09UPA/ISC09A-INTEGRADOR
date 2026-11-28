namespace AlmacenInteligente.Application.Seguridad;

public static class Roles
{
    public const string Superadmin = "Superadmin";
    public const string AgenteVentas = "AgenteVentas";
    public const string EncargadoRecepciones = "EncargadoRecepciones";
    public const string Gerente = "Gerente";

    public static readonly IReadOnlyList<string> Todos = [Superadmin, AgenteVentas, EncargadoRecepciones, Gerente];

    /// <summary>Nombre para mostrar en pantalla.</summary>
    public static string Nombre(string rol) => rol switch
    {
        Superadmin => "Superadmin",
        AgenteVentas => "Agente de ventas",
        EncargadoRecepciones => "Encargado de recepciones",
        Gerente => "Gerente",
        _ => rol,
    };
}
