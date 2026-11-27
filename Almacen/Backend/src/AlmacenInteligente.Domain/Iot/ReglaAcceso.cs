namespace AlmacenInteligente.Domain.Iot;

/// <summary>Una etiqueta del modelo de Custom Vision (el Id del usuario) y su probabilidad.</summary>
public sealed record PrediccionRostro(string Etiqueta, double Probabilidad);

public sealed record DecisionAcceso(bool Permitido, double Confianza, string Motivo);

/// <summary>
/// Decide si se abre el acceso: la tarjeta dice quién dice ser la persona y la cámara lo confirma.
/// Se permite solo si la etiqueta más probable es la del dueño de la tarjeta y su probabilidad alcanza el umbral.
/// </summary>
public static class ReglaAcceso
{
    public static DecisionAcceso Decidir(Guid usuarioId, IEnumerable<PrediccionRostro> predicciones, double umbral)
    {
        if (umbral is <= 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(umbral), "El umbral debe estar entre 0 y 1.");

        var lista = predicciones.ToList();
        var mejor = lista.MaxBy(p => p.Probabilidad);
        if (mejor is null)
            return new DecisionAcceso(false, 0, "No se reconoció ningún rostro");

        var etiqueta = usuarioId.ToString();
        var delUsuario = lista.Where(p => EsDelUsuario(p, etiqueta)).Select(p => p.Probabilidad).DefaultIfEmpty(0).Max();

        if (!EsDelUsuario(mejor, etiqueta))
            return new DecisionAcceso(false, delUsuario, "El rostro no coincide con el de la tarjeta");
        if (mejor.Probabilidad < umbral)
            return new DecisionAcceso(false, mejor.Probabilidad, $"Confianza insuficiente ({mejor.Probabilidad:P0}, se requiere {umbral:P0})");
        return new DecisionAcceso(true, mejor.Probabilidad, "Acceso permitido");
    }

    private static bool EsDelUsuario(PrediccionRostro p, string etiqueta) =>
        string.Equals(p.Etiqueta, etiqueta, StringComparison.OrdinalIgnoreCase);
}
