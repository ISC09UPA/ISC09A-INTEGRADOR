namespace CiudadInteligente.Domain.Enums;

/// <summary>
/// Los 4 accesos del cruce. El numero es el mismo que usa el protocolo
/// serial del ESP32 (SET:1:VERDE, AMBULANCIA:3, ...).
/// Ejes: Norte-Sur (1, 2) y Este-Oeste (3, 4).
/// </summary>
public enum Acceso
{
    Norte = 1,
    Sur = 2,
    Este = 3,
    Oeste = 4
}

public static class AccesoReglas
{
    public const int Cantidad = 4;

    public static bool EsValido(int numero) => numero is >= 1 and <= Cantidad;

    public static string Nombre(int numero) =>
        EsValido(numero) ? ((Acceso)numero).ToString() : "Ninguno";

    /// <summary>True para Norte y Sur; false para Este y Oeste.</summary>
    public static bool EsEjeNorteSur(int numero) => numero is 1 or 2;
}
