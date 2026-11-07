using CiudadInteligente.Domain.Enums;

namespace CiudadInteligente.Domain.Entities;

/// <summary>
/// Vehiculos detectados por la camara en cada acceso en un momento dado.
/// Lo manda el script Embebidos/RaspberryPi/vision y se guarda en la tabla
/// ConteosTrafico (SQLite).
/// </summary>
public class ConteoTrafico
{
    public long Id { get; set; }

    public DateTimeOffset RegistradoEn { get; set; }

    public int Norte { get; set; }
    public int Sur { get; set; }
    public int Este { get; set; }
    public int Oeste { get; set; }

    public int Total => Norte + Sur + Este + Oeste;

    /// <summary>En el orden del protocolo: indice 0 = acceso 1 (Norte) ... 3 = Oeste.</summary>
    public int[] ComoArreglo() => new[] { Norte, Sur, Este, Oeste };

    public static ConteoTrafico Desde(IReadOnlyList<int> vehiculos, DateTimeOffset registradoEn)
    {
        if (vehiculos.Count != AccesoReglas.Cantidad)
        {
            throw new ArgumentException($"Se esperaban {AccesoReglas.Cantidad} valores (uno por acceso).", nameof(vehiculos));
        }

        if (vehiculos.Any(v => v < 0))
        {
            throw new ArgumentException("El conteo de vehiculos no puede ser negativo.", nameof(vehiculos));
        }

        return new ConteoTrafico
        {
            RegistradoEn = registradoEn,
            Norte = vehiculos[0],
            Sur = vehiculos[1],
            Este = vehiculos[2],
            Oeste = vehiculos[3]
        };
    }
}
