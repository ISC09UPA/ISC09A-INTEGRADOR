using Microsoft.EntityFrameworkCore;

namespace InvernaderoInteligente.Infrastructure.Data;

/// <summary>Ayuda a reconocer errores de la base de datos sin depender del proveedor.</summary>
internal static class ErroresBd
{
    /// <summary>True si el error es por un índice único (registro duplicado).</summary>
    public static bool EsDuplicado(DbUpdateException ex)
    {
        var mensaje = ex.InnerException?.Message ?? ex.Message;

        return mensaje.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) // SQLite
            || mensaje.Contains("duplicate key", StringComparison.OrdinalIgnoreCase);           // SQL Server (2601 / 2627)
    }
}
