using System.Globalization;
using AlmacenInteligente.Application.Consultas;
using ScottPlot;

namespace AlmacenInteligente.Infrastructure.Pdf;

/// <summary>Gráficas de los reportes PDF como imágenes PNG (ScottPlot), con el mismo tono que la web.</summary>
internal static class Graficas
{
    private static readonly CultureInfo Mx = CultureInfo.GetCultureInfo("es-MX");
    private static readonly Color Primario = Color.FromHex("#1F5F8B");
    private static readonly Color Secundario = Color.FromHex("#2E8B57");
    private static readonly Color Error = Color.FromHex("#C0392B");

    private static readonly Color[] Paleta =
    [
        Color.FromHex("#1F5F8B"), Color.FromHex("#2E8B57"), Color.FromHex("#D98E04"), Color.FromHex("#8E44AD"),
        Color.FromHex("#C0392B"), Color.FromHex("#16A085"), Color.FromHex("#5B6B7A"), Color.FromHex("#E67E22"),
    ];

    private const int Ancho = 1000;
    private const int Alto = 380;

    public static byte[] Barras(IReadOnlyList<PuntoDto> puntos, string? titulo = null) =>
        BarrasAgrupadas(puntos.Select(p => p.Etiqueta).ToList(), [(titulo ?? "", puntos.Select(p => (double)p.Valor).ToArray(), Primario)], leyenda: false);

    public static byte[] Linea(IReadOnlyList<PuntoDto> puntos)
    {
        var plt = Nueva();
        var xs = Enumerable.Range(0, puntos.Count).Select(i => (double)i).ToArray();
        var linea = plt.Add.Scatter(xs, puntos.Select(p => (double)p.Valor).ToArray());
        linea.Color = Primario;
        linea.LineWidth = 2.5f;
        linea.MarkerSize = puntos.Count <= 31 ? 5 : 0;
        Etiquetas(plt, puntos.Select(p => p.Etiqueta).ToList());
        plt.Axes.Margins(bottom: 0);
        return plt.GetImageBytes(Ancho, Alto, ImageFormat.Png);
    }

    /// <summary>Compras contra ventas por periodo, una barra junto a la otra.</summary>
    public static byte[] IngresosEgresos(ReporteFinancieroDto f) =>
        BarrasAgrupadas(f.Periodos,
        [
            ("Ventas (ingresos)", f.Ingresos.Select(x => (double)x).ToArray(), Secundario),
            ("Compras (egresos)", f.Egresos.Select(x => (double)x).ToArray(), Error),
        ], leyenda: true);

    /// <summary>Un grupo de barras por mes (E…D) con una barra por año, como en el reporte anual de Dolibarr.</summary>
    public static byte[] PorMesYAnio(IReadOnlyList<int> anios, Func<int, int, decimal> valor)
    {
        string[] meses = ["E", "F", "M", "A", "M", "J", "J", "A", "S", "O", "N", "D"];
        var series = anios.Select((anio, i) => (
            anio.ToString(),
            Enumerable.Range(1, 12).Select(mes => (double)valor(anio, mes)).ToArray(),
            Paleta[i % Paleta.Length])).ToList();
        return BarrasAgrupadas(meses, series, leyenda: true, alto: 700);
    }

    public static byte[] Pastel(IReadOnlyList<PuntoDto> puntos)
    {
        var plt = new Plot();
        var rebanadas = puntos.Where(p => p.Valor > 0).Select((p, i) => new PieSlice
        {
            Value = (double)p.Valor,
            Label = p.Etiqueta,
            LegendText = $"{p.Etiqueta}: {p.Valor.ToString("C0", Mx)}",
            FillColor = Paleta[i % Paleta.Length],
        }).ToList();
        var pastel = plt.Add.Pie(rebanadas);
        pastel.DonutFraction = 0.5;
        pastel.SliceLabelDistance = 1.25;
        plt.Axes.Frameless();
        plt.HideGrid();
        plt.ShowLegend(Edge.Right);
        return plt.GetImageBytes(Ancho, Alto, ImageFormat.Png);
    }

    private static byte[] BarrasAgrupadas(IReadOnlyList<string> etiquetas, IReadOnlyList<(string Nombre, double[] Valores, Color Color)> series, bool leyenda, int alto = Alto)
    {
        var plt = Nueva();
        var ancho = 0.8 / series.Count;
        for (var s = 0; s < series.Count; s++)
        {
            var (nombre, valores, color) = series[s];
            var barras = valores.Select((v, i) => new Bar
            {
                Position = i - 0.4 + ancho * (s + 0.5),
                Value = v,
                Size = ancho * 0.9,
                FillColor = color,
                LineWidth = 0,
            }).ToList();
            var grafica = plt.Add.Bars(barras);
            grafica.LegendText = nombre;
        }
        Etiquetas(plt, etiquetas);
        plt.Axes.Margins(bottom: 0);
        if (leyenda)
            plt.ShowLegend(Alignment.UpperLeft);
        return plt.GetImageBytes(Ancho, alto, ImageFormat.Png);
    }

    private static Plot Nueva()
    {
        var plt = new Plot();
        plt.Axes.Left.TickGenerator = new ScottPlot.TickGenerators.NumericAutomatic
        {
            LabelFormatter = v => v.ToString("C0", Mx),
        };
        plt.Grid.MajorLineColor = Color.FromHex("#E9EDF1");
        plt.Axes.Right.IsVisible = false;
        plt.Axes.Top.IsVisible = false;
        return plt;
    }

    /// <summary>Etiquetas del eje X; con muchos periodos se muestran algunas para que no se encimen.</summary>
    private static void Etiquetas(Plot plt, IReadOnlyList<string> etiquetas)
    {
        var paso = Math.Max(1, (int)Math.Ceiling(etiquetas.Count / 14.0));
        var posiciones = new List<double>();
        var textos = new List<string>();
        for (var i = 0; i < etiquetas.Count; i += paso)
        {
            posiciones.Add(i);
            textos.Add(etiquetas[i]);
        }
        plt.Axes.Bottom.SetTicks(posiciones.ToArray(), textos.ToArray());
        plt.Axes.Bottom.MajorTickStyle.Length = 0;
        plt.Axes.Bottom.TickLabelStyle.FontSize = 13;
        // Solo se inclinan cuando no caben derechas; el eje reserva espacio para que no se corten.
        if (textos.Count * textos.Max(t => t.Length) > 90)
        {
            plt.Axes.Bottom.TickLabelStyle.Rotation = -35;
            plt.Axes.Bottom.TickLabelStyle.Alignment = Alignment.MiddleRight;
            plt.Axes.Bottom.MinimumSize = 70;
        }
    }
}
