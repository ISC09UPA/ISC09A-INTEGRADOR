namespace AlmacenInteligente.Infrastructure.Pdf;

/// <summary>Importe con letra al estilo de los comprobantes mexicanos: "MIL DOSCIENTOS DIECIOCHO PESOS 00/100 M.N.".</summary>
internal static class NumeroALetras
{
    private static readonly string[] Unidades =
    [
        "", "UN", "DOS", "TRES", "CUATRO", "CINCO", "SEIS", "SIETE", "OCHO", "NUEVE", "DIEZ",
        "ONCE", "DOCE", "TRECE", "CATORCE", "QUINCE", "DIECISÉIS", "DIECISIETE", "DIECIOCHO", "DIECINUEVE", "VEINTE",
        "VEINTIÚN", "VEINTIDÓS", "VEINTITRÉS", "VEINTICUATRO", "VEINTICINCO", "VEINTISÉIS", "VEINTISIETE", "VEINTIOCHO", "VEINTINUEVE",
    ];

    private static readonly string[] Decenas = ["", "", "", "TREINTA", "CUARENTA", "CINCUENTA", "SESENTA", "SETENTA", "OCHENTA", "NOVENTA"];

    private static readonly string[] Centenas =
        ["", "CIENTO", "DOSCIENTOS", "TRESCIENTOS", "CUATROCIENTOS", "QUINIENTOS", "SEISCIENTOS", "SETECIENTOS", "OCHOCIENTOS", "NOVECIENTOS"];

    public static string Pesos(decimal importe)
    {
        var redondeado = Math.Round(Math.Abs(importe), 2, MidpointRounding.AwayFromZero);
        var enteros = (long)decimal.Truncate(redondeado);
        var centavos = (int)((redondeado - enteros) * 100);
        var letras = enteros == 0 ? "CERO" : Convertir(enteros);
        var moneda = enteros == 1 ? "PESO" : (enteros % 1_000_000 == 0 ? "DE PESOS" : "PESOS");
        return $"{letras} {moneda} {centavos:D2}/100 M.N.";
    }

    private static string Convertir(long n)
    {
        if (n >= 1_000_000)
        {
            var millones = n / 1_000_000;
            var resto = n % 1_000_000;
            var texto = millones == 1 ? "UN MILLÓN" : $"{Convertir(millones)} MILLONES";
            return resto == 0 ? texto : $"{texto} {Convertir(resto)}";
        }
        if (n >= 1000)
        {
            var miles = n / 1000;
            var resto = n % 1000;
            var texto = miles == 1 ? "MIL" : $"{Convertir(miles)} MIL";
            return resto == 0 ? texto : $"{texto} {Convertir(resto)}";
        }
        if (n >= 100)
        {
            if (n == 100)
                return "CIEN";
            var resto = n % 100;
            return resto == 0 ? Centenas[n / 100] : $"{Centenas[n / 100]} {Convertir(resto)}";
        }
        if (n < 30)
            return Unidades[n];
        var unidad = n % 10;
        return unidad == 0 ? Decenas[n / 10] : $"{Decenas[n / 10]} Y {Unidades[unidad]}";
    }
}
