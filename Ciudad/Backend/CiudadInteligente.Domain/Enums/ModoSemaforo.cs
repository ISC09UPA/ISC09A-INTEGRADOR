namespace CiudadInteligente.Domain.Enums;

/// <summary>Modo de operacion del cruce.</summary>
public enum ModoSemaforo
{
    /// <summary>Ciclo automatico de los 4 accesos (lo corre el ESP32 solo).</summary>
    Auto,

    /// <summary>La app controla cada acceso a mano.</summary>
    Manual,

    /// <summary>Corredor de emergencia: un acceso en verde, los demas en rojo.</summary>
    Ambulancia
}
