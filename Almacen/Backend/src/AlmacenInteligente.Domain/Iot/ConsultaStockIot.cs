using AlmacenInteligente.Domain.Inventario;

namespace AlmacenInteligente.Domain.Iot;

/// <summary>
/// Bitácora de cada comprobación de stock hecha desde el lector del embebido: una pieza que pasó por el lector
/// mientras nadie tenía el modo inteligente de venta. Guarda lo que se le respondió (existencia total y nivel).
/// </summary>
public sealed class ConsultaStockIot : Common.Entity
{
    public DateTimeOffset Fecha { get; private set; }
    public string Uid { get; private set; } = null!;
    public string Familia { get; private set; } = null!;

    /// <summary>Null si la familia no corresponde a ningún producto.</summary>
    public Guid? ProductoId { get; private set; }
    public decimal? Existencia { get; private set; }
    public decimal? Minimo { get; private set; }
    public NivelStock? Nivel { get; private set; }
    public string Motivo { get; private set; } = null!;

    /// <summary>Almacén de quien consultó (null = todos, superadmin) y el usuario con sesión en el embebido.</summary>
    public Guid? AlmacenId { get; private set; }
    public Guid? UsuarioId { get; private set; }

    private ConsultaStockIot() { }

    public ConsultaStockIot(DateTimeOffset fecha, string uid, string familia, Guid? productoId, decimal? existencia, decimal? minimo, string motivo,
        Guid? almacenId = null, Guid? usuarioId = null)
    {
        AlmacenId = almacenId;
        UsuarioId = usuarioId;
        Fecha = fecha;
        Uid = Recortar(uid, CodigoNfc.LongitudMaxima);
        Familia = Recortar(familia, EscaneoIot.LongitudMaximaFamilia);
        ProductoId = productoId;
        Existencia = existencia;
        Minimo = minimo;
        Nivel = existencia is { } e ? SemaforoStock.Nivel(e, minimo ?? 0) : null;
        Motivo = Recortar(motivo, 300);
    }

    private static string Recortar(string valor, int maximo) => valor.Length > maximo ? valor[..maximo] : valor;
}
