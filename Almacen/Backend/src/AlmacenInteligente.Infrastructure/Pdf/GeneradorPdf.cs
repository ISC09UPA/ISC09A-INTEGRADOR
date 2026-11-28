using System.Globalization;
using AlmacenInteligente.Application.Consultas;
using AlmacenInteligente.Application.Pdf;
using AlmacenInteligente.Domain.Documentos;
using AlmacenInteligente.Domain.Inventario;
using Microsoft.Extensions.Configuration;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AlmacenInteligente.Infrastructure.Pdf;

/// <summary>Datos de la empresa que aparecen en el encabezado (sección "Empresa" de la configuración).</summary>
internal sealed record Empresa(string Nombre, string? Rfc, string? Direccion, string? Telefono, string? Correo)
{
    public static Empresa Desde(IConfiguration c) => new(
        c["Empresa:Nombre"] ?? "Almacén inteligente", c["Empresa:Rfc"], c["Empresa:Direccion"], c["Empresa:Telefono"], c["Empresa:Correo"]);
}

/// <summary>Comprobantes y reportes en PDF con QuestPDF. Tamaño carta, español de México.</summary>
internal sealed class GeneradorPdf : IGeneradorPdf
{
    private static readonly CultureInfo Mx = CultureInfo.GetCultureInfo("es-MX");
    private static readonly TimeZoneInfo ZonaMexico = TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");

    private const string Primario = "#1F5F8B";
    private const string Oscuro = "#14324A";
    private const string Gris = "#5B6B7A";
    private const string Linea = "#E1E6EB";
    private const string Fondo = "#F4F6F8";
    private const string Rojo = "#B32121";

    private readonly Empresa _empresa;

    static GeneradorPdf() => QuestPDF.Settings.License = LicenseType.Community;

    public GeneradorPdf(IConfiguration configuracion) => _empresa = Empresa.Desde(configuracion);

    // =============================================================== Comprobantes

    public byte[] Comprobante(TipoDocumento tipo, DocumentoDetalleDto d, DateTimeOffset generadoEn)
    {
        var (titulo, prefijo, etiquetaTercero, etiquetaPrecio) = tipo switch
        {
            TipoDocumento.Venta => ("Comprobante de venta", "VTA", "Cliente", "Precio unit."),
            TipoDocumento.Recepcion => ("Comprobante de recepción", "REC", "Proveedor", "Costo unit."),
            _ => ("Comprobante de traspaso", "TRA", null, "Costo unit."),
        };
        var conImportes = tipo != TipoDocumento.Traspaso;
        var almacenPorLinea = tipo == TipoDocumento.Venta;

        return Document.Create(doc => doc.Page(page =>
        {
            Pagina(page);
            page.Header().Element(c => Encabezado(c, titulo, $"{prefijo}-{d.Folio:D5}", $"Fecha: {d.Fecha.ToString("dd 'de' MMMM 'de' yyyy", Mx)}"));

            page.Content().PaddingVertical(12).Column(col =>
            {
                col.Spacing(12);

                if (d.Estado == EstadoDocumento.Cancelado)
                    col.Item().Background("#FDECEC").Border(1).BorderColor(Rojo).Padding(8).Column(c =>
                    {
                        c.Item().Text("DOCUMENTO CANCELADO").Bold().FontColor(Rojo);
                        c.Item().Text($"Canceló {d.CanceladoPor} el {FechaHora(d.CanceladoEn)}. Motivo: {d.MotivoCancelacion}").FontSize(8);
                    });

                col.Item().Row(row =>
                {
                    row.Spacing(12);
                    if (etiquetaTercero is not null)
                        row.RelativeItem(3).Element(Caja).Column(c =>
                        {
                            c.Item().Text(etiquetaTercero.ToUpperInvariant()).FontSize(7).FontColor(Gris).SemiBold();
                            c.Item().Text(d.Tercero).FontSize(11).Bold();
                            c.Item().Text($"RFC: {d.TerceroRfc}");
                            c.Item().Text(d.TerceroCorreo).FontColor(Gris);
                        });
                    row.RelativeItem(2).Element(Caja).Column(c =>
                    {
                        Dato(c, d.AlmacenDestino is null ? "Almacén" : "Almacén de origen", d.Almacen);
                        if (d.AlmacenDestino is not null)
                            Dato(c, "Almacén de destino", d.AlmacenDestino);
                        Dato(c, "Registró", $"{d.CreadoPor} · {FechaHora(d.CreadoEn)}");
                        Dato(c, "Estado", d.Estado == EstadoDocumento.Cancelado ? "Cancelado" : "Confirmado");
                    });
                });

                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(cd =>
                    {
                        cd.ConstantColumn(22);
                        cd.ConstantColumn(70);
                        cd.RelativeColumn(4);
                        if (almacenPorLinea) cd.RelativeColumn(2);
                        cd.ConstantColumn(62);
                        cd.ConstantColumn(66);
                        if (conImportes) { cd.ConstantColumn(36); cd.ConstantColumn(72); }
                    });

                    t.Header(h =>
                    {
                        Th(h.Cell(), "#");
                        Th(h.Cell(), "Referencia");
                        Th(h.Cell(), "Descripción");
                        if (almacenPorLinea) Th(h.Cell(), "Almacén");
                        Th(h.Cell(), "Cantidad", derecha: true);
                        Th(h.Cell(), etiquetaPrecio, derecha: true);
                        if (conImportes) { Th(h.Cell(), "IVA", derecha: true); Th(h.Cell(), "Importe", derecha: true); }
                    });

                    var n = 0;
                    foreach (var l in d.Lineas)
                    {
                        n++;
                        Td(t.Cell(), n.ToString());
                        Td(t.Cell(), l.Referencia, negrita: true);
                        Td(t.Cell(), l.Descripcion);
                        if (almacenPorLinea) Td(t.Cell(), l.Almacen ?? "");
                        Td(t.Cell(), $"{Cantidad(l.Cantidad)} {l.Unidad}", derecha: true);
                        Td(t.Cell(), Costo(l.PrecioUnitario), derecha: true);
                        if (conImportes) { Td(t.Cell(), $"{l.TasaIva * 100:0.##}%", derecha: true); Td(t.Cell(), Dinero(l.Subtotal), derecha: true); }
                    }
                });

                if (conImportes)
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().PaddingRight(16).Column(c =>
                        {
                            c.Item().Text("Importe con letra").FontSize(7).FontColor(Gris).SemiBold();
                            c.Item().Text(NumeroALetras.Pesos(d.Total)).FontSize(8);
                        });
                        row.ConstantItem(200).Element(Caja).Column(c =>
                        {
                            Total(c, "Subtotal", Dinero(d.Subtotal));
                            Total(c, "IVA", Dinero(d.Iva));
                            c.Item().PaddingTop(4).BorderTop(1).BorderColor(Linea).PaddingTop(4).Row(r =>
                            {
                                r.RelativeItem().Text("Total").Bold().FontSize(11);
                                r.AutoItem().Text(Dinero(d.Total)).Bold().FontSize(11).FontColor(Primario);
                            });
                        });
                    });
                }
                else
                {
                    col.Item().AlignRight().Text($"Valor trasladado al costo: {Dinero(d.Lineas.Sum(l => l.Cantidad * l.CostoUnitario))}").SemiBold();
                }

                if (tipo == TipoDocumento.Traspaso)
                    col.Item().PaddingTop(30).Row(row =>
                    {
                        row.Spacing(40);
                        Firma(row.RelativeItem(), "Entrega (origen)");
                        Firma(row.RelativeItem(), "Recibe (destino)");
                    });
            });

            page.Footer().Element(c => Pie(c, generadoEn, "Este documento es un comprobante interno y no sustituye a un CFDI."));
        })).GeneratePdf();
    }

    // =============================================================== Reportes

    public byte[] Reporte(DatosReporte r)
    {
        var titulo = r.Tipo switch
        {
            TipoReporte.Ventas => "Reporte de ventas",
            TipoReporte.Compras => "Reporte de compras",
            TipoReporte.Inventario => "Reporte de inventario",
            TipoReporte.Financiero => "Flujo financiero",
            TipoReporte.Margen => "Margen por producto",
            _ => "Reporte general",
        };
        var periodo = r.Tipo == TipoReporte.Inventario
            ? $"Existencias al {FechaHora(r.GeneradoEn)}"
            : $"Periodo: {r.Desde.ToString("dd MMM yyyy", Mx)} – {r.Hasta.ToString("dd MMM yyyy", Mx)}";

        return Document.Create(doc => doc.Page(page =>
        {
            Pagina(page);
            page.Header().Element(c => Encabezado(c, titulo, null, periodo));
            page.Content().PaddingVertical(12).Column(col =>
            {
                col.Spacing(16);
                if (r.Ventas is { } v) SeccionVentas(col, v);
                if (r.Compras is { } c) SeccionCompras(col, c);
                if (r.Financiero is { } f) SeccionFinanciera(col, f);
                if (r.Inventario is { } i) SeccionInventario(col, i);
                if (r.Margen is { } m) SeccionMargen(col, m);
            });
            page.Footer().Element(c => Pie(c, r.GeneradoEn, $"Generado por {r.GeneradoPor}. Ventas y recepciones canceladas no se incluyen."));
        })).GeneratePdf();
    }

    private static void SeccionVentas(ColumnDescriptor col, ReporteVentasDto v)
    {
        Titulo(col, "Ventas");
        Indicadores(col, ("Total vendido", Dinero(v.Total)), ("Ventas", v.NumeroVentas.ToString("N0", Mx)),
            ("Ticket promedio", Dinero(v.TicketPromedio)), ("IVA trasladado", Dinero(v.Iva)));
        Grafica(col, "Ventas por periodo (total con IVA)", Graficas.Linea(v.PorPeriodo));
        col.Item().Row(row =>
        {
            row.Spacing(12);
            row.RelativeItem().Element(c => TablaTop(c, "Top 10 clientes", v.TopClientes));
            row.RelativeItem().Element(c => TablaTop(c, "Top 10 productos (subtotal)", v.TopProductos));
        });
        if (v.PorAlmacen.Count > 0)
            Grafica(col, "Ventas por almacén (subtotal)", Graficas.Pastel(v.PorAlmacen));
    }

    private static void SeccionCompras(ColumnDescriptor col, ReporteComprasDto c)
    {
        Titulo(col, "Compras (recepciones)");
        Indicadores(col, ("Total comprado", Dinero(c.Total)), ("Recepciones", c.NumeroRecepciones.ToString("N0", Mx)),
            ("Subtotal", Dinero(c.Subtotal)), ("IVA acreditable", Dinero(c.Iva)));
        Grafica(col, "Compras por periodo (total con IVA)", Graficas.Barras(c.PorPeriodo));
        col.Item().Element(x => TablaTop(x, "Compras por proveedor", c.PorProveedor));
    }

    private static void SeccionFinanciera(ColumnDescriptor col, ReporteFinancieroDto f)
    {
        Titulo(col, "Flujo financiero");
        Indicadores(col, ("Ingresos", Dinero(f.TotalIngresos)), ("Egresos", Dinero(f.TotalEgresos)),
            ("Saldo", Dinero(f.Saldo)), ("IVA por pagar", Dinero(f.IvaTrasladado - f.IvaAcreditable)));
        Grafica(col, "Compras contra ventas por periodo", Graficas.IngresosEgresos(f));
        col.Item().Text($"IVA trasladado {Dinero(f.IvaTrasladado)} · IVA acreditable {Dinero(f.IvaAcreditable)}. Incluye los reversos de documentos cancelados.")
            .FontSize(8).FontColor(Gris);
    }

    private static void SeccionInventario(ColumnDescriptor col, ReporteInventarioDto i)
    {
        Titulo(col, "Inventario");
        Indicadores(col, ("Valor del inventario", Dinero(i.ValorTotal)), ("Almacenes", i.ValorPorAlmacen.Count.ToString()),
            ("Productos bajo mínimo", i.BajoMinimo.Count.ToString()));
        if (i.ValorPorAlmacen.Count > 0)
            Grafica(col, "Valor por almacén (costo promedio)", Graficas.Pastel(i.ValorPorAlmacen));

        col.Item().Text("Productos en o por debajo del mínimo").SemiBold();
        if (i.BajoMinimo.Count == 0)
        {
            col.Item().Text("Ningún producto está en o por debajo de su mínimo.").FontColor(Gris);
            return;
        }
        col.Item().Table(t =>
        {
            t.ColumnsDefinition(cd => { cd.RelativeColumn(2); cd.ConstantColumn(70); cd.RelativeColumn(4); cd.ConstantColumn(70); cd.ConstantColumn(70); });
            t.Header(h =>
            {
                Th(h.Cell(), "Almacén"); Th(h.Cell(), "Referencia"); Th(h.Cell(), "Descripción");
                Th(h.Cell(), "Existencia", derecha: true); Th(h.Cell(), "Mínimo", derecha: true);
            });
            foreach (var e in i.BajoMinimo)
            {
                Td(t.Cell(), e.Almacen); Td(t.Cell(), e.Referencia, negrita: true); Td(t.Cell(), e.Descripcion);
                Td(t.Cell(), $"{Cantidad(e.Cantidad)} {e.Unidad}", derecha: true); Td(t.Cell(), Cantidad(e.StockMinimo), derecha: true);
            }
        });
    }

    private static void SeccionMargen(ColumnDescriptor col, IReadOnlyList<MargenProductoDto> m)
    {
        Titulo(col, "Margen por producto");
        if (m.Count == 0)
        {
            col.Item().Text("No hay ventas en el periodo.").FontColor(Gris);
            return;
        }
        Indicadores(col, ("Ventas", Dinero(m.Sum(x => x.Ventas))), ("Costo", Dinero(m.Sum(x => x.Costo))),
            ("Margen", Dinero(m.Sum(x => x.Margen))),
            ("Margen %", m.Sum(x => x.Ventas) == 0 ? "—" : $"{m.Sum(x => x.Margen) / m.Sum(x => x.Ventas) * 100:0.0} %"));
        Grafica(col, "Margen de los 10 productos principales",
            Graficas.Barras(m.Take(10).Select(x => new PuntoDto(x.Referencia, x.Margen)).ToList()));
        col.Item().Table(t =>
        {
            t.ColumnsDefinition(cd => { cd.ConstantColumn(70); cd.RelativeColumn(4); cd.ConstantColumn(55); cd.ConstantColumn(70); cd.ConstantColumn(70); cd.ConstantColumn(70); cd.ConstantColumn(42); });
            t.Header(h =>
            {
                Th(h.Cell(), "Referencia"); Th(h.Cell(), "Descripción"); Th(h.Cell(), "Cantidad", derecha: true);
                Th(h.Cell(), "Ventas", derecha: true); Th(h.Cell(), "Costo", derecha: true); Th(h.Cell(), "Margen", derecha: true); Th(h.Cell(), "%", derecha: true);
            });
            foreach (var p in m)
            {
                Td(t.Cell(), p.Referencia, negrita: true); Td(t.Cell(), p.Descripcion); Td(t.Cell(), Cantidad(p.Cantidad), derecha: true);
                Td(t.Cell(), Dinero(p.Ventas), derecha: true); Td(t.Cell(), Dinero(p.Costo), derecha: true);
                Td(t.Cell(), Dinero(p.Margen), derecha: true, negrita: true); Td(t.Cell(), $"{p.MargenPorcentaje:0.0}", derecha: true);
            }
        });
    }

    // =============================================================== Reporte mensual financiero

    public byte[] ReporteMensual(Application.Finanzas.ReporteMensualDto r, DateTimeOffset generadoEn, string generadoPor) =>
        Document.Create(doc =>
        {
            doc.Page(page =>
            {
                Pagina(page);
                page.Size(PageSizes.Letter.Landscape());
                page.Header().Element(c => Encabezado(c, "Reporte mensual", null, $"Años {r.Anios[0]} – {r.Anios[^1]} · MXN"));
                page.Content().PaddingVertical(12).Column(col =>
                {
                    col.Spacing(10);
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(cd =>
                        {
                            cd.RelativeColumn(1.4f);
                            foreach (var _ in r.Anios) { cd.RelativeColumn(); cd.RelativeColumn(); }
                        });
                        t.Header(h =>
                        {
                            h.Cell().RowSpan(2).Element(c => Th(c, "Mes"));
                            foreach (var anio in r.Anios)
                                h.Cell().ColumnSpan(2).Background(Oscuro).PaddingVertical(4).AlignCenter().Text(anio.ToString()).FontColor(Colors.White).Bold();
                            foreach (var _ in r.Anios)
                            {
                                Th(h.Cell(), "Debe", derecha: true);
                                Th(h.Cell(), "Haber", derecha: true);
                            }
                        });
                        foreach (var mes in r.Meses)
                        {
                            Td(t.Cell(), mes.Nombre);
                            foreach (var v in mes.PorAnio)
                            {
                                Td(t.Cell(), v.Debe == 0 ? "" : Dinero(v.Debe), derecha: true);
                                Td(t.Cell(), v.Haber == 0 ? "" : Dinero(v.Haber), derecha: true);
                            }
                        }
                        Td(t.Cell(), "Total", negrita: true);
                        foreach (var v in r.Totales)
                        {
                            Td(t.Cell(), Dinero(v.Debe), derecha: true, negrita: true);
                            Td(t.Cell(), Dinero(v.Haber), derecha: true, negrita: true);
                        }
                    });
                    col.Item().Element(Caja).Row(row =>
                    {
                        row.RelativeItem().Text("Saldo actual (ingresos menos egresos desde el inicio)").SemiBold();
                        row.AutoItem().Text(Dinero(r.SaldoActual)).Bold().FontSize(12).FontColor(r.SaldoActual < 0 ? Rojo : Primario);
                    });
                    col.Item().Text("Debe = egresos por compras (recepciones). Haber = ingresos por ventas. Los reversos de documentos cancelados restan en el mes de la cancelación.")
                        .FontSize(7).FontColor(Gris);
                });
                page.Footer().Element(c => Pie(c, generadoEn, $"Generado por {generadoPor}."));
            });

            doc.Page(page =>
            {
                Pagina(page);
                page.Size(PageSizes.Letter.Landscape());
                page.Header().Element(c => Encabezado(c, "Reporte mensual · gráficos", null, $"Años {r.Anios[0]} – {r.Anios[^1]} · MXN"));
                page.Content().PaddingVertical(12).Row(row =>
                {
                    row.Spacing(16);
                    decimal Valor(int anio, int mes, bool debe)
                    {
                        var v = r.Meses[mes - 1].PorAnio.First(x => x.Anio == anio);
                        return debe ? v.Debe : v.Haber;
                    }
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text($"Debe (egresos) · {string.Join(" - ", r.Anios)}").SemiBold();
                        c.Item().PaddingTop(4).Image(Graficas.PorMesYAnio(r.Anios, (a, m) => Valor(a, m, true))).FitWidth();
                    });
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text($"Haber (ingresos) · {string.Join(" - ", r.Anios)}").SemiBold();
                        c.Item().PaddingTop(4).Image(Graficas.PorMesYAnio(r.Anios, (a, m) => Valor(a, m, false))).FitWidth();
                    });
                });
                page.Footer().Element(c => Pie(c, generadoEn, $"Generado por {generadoPor}."));
            });
        }).GeneratePdf();

    // =============================================================== Movimientos de inventario

    public byte[] Movimientos(Application.Movimientos.FiltroMovimientos f, Application.Movimientos.ReporteMovimientosDto r,
        DateTimeOffset generadoEn, string generadoPor) =>
        Document.Create(doc => doc.Page(page =>
        {
            Pagina(page);
            page.Size(PageSizes.Letter.Landscape());
            page.Header().Element(c => Encabezado(c, "Movimientos de inventario", null,
                $"Periodo: {f.Desde.ToString("dd MMM yyyy", Mx)} – {f.Hasta.ToString("dd MMM yyyy", Mx)}"));

            page.Content().PaddingVertical(12).Column(col =>
            {
                col.Spacing(14);
                Indicadores(col,
                    ("Movimientos", r.NumeroMovimientos.ToString("N0", Mx)),
                    ("Unidades que entraron", Cantidad(r.Entradas)), ("Valor de entradas", Dinero(r.ValorEntradas)),
                    ("Unidades que salieron", Cantidad(r.Salidas)), ("Valor de salidas", Dinero(r.ValorSalidas)));

                Titulo(col, "Resumen por producto y almacén");
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(cd =>
                    {
                        cd.RelativeColumn(2); cd.ConstantColumn(75); cd.RelativeColumn(4);
                        cd.ConstantColumn(65); cd.ConstantColumn(65); cd.ConstantColumn(65); cd.ConstantColumn(65);
                        cd.ConstantColumn(80); cd.ConstantColumn(80);
                    });
                    t.Header(h =>
                    {
                        Th(h.Cell(), "Almacén"); Th(h.Cell(), "Referencia"); Th(h.Cell(), "Descripción");
                        Th(h.Cell(), "Inicial", derecha: true); Th(h.Cell(), "Entradas", derecha: true);
                        Th(h.Cell(), "Salidas", derecha: true); Th(h.Cell(), "Final", derecha: true);
                        Th(h.Cell(), "Valor entradas", derecha: true); Th(h.Cell(), "Valor salidas", derecha: true);
                    });
                    foreach (var p in r.PorProducto)
                    {
                        Td(t.Cell(), p.Almacen); Td(t.Cell(), p.Referencia, negrita: true); Td(t.Cell(), p.Descripcion);
                        Td(t.Cell(), Cantidad(p.Inicial), derecha: true); Td(t.Cell(), Cantidad(p.Entradas), derecha: true);
                        Td(t.Cell(), Cantidad(p.Salidas), derecha: true); Td(t.Cell(), Cantidad(p.Final), derecha: true, negrita: true);
                        Td(t.Cell(), Dinero(p.ValorEntradas), derecha: true); Td(t.Cell(), Dinero(p.ValorSalidas), derecha: true);
                    }
                });

                Titulo(col, "Detalle de movimientos");
                if (r.Movimientos.Elementos.Count < r.NumeroMovimientos)
                    col.Item().Text($"Se muestran {r.Movimientos.Elementos.Count:N0} de {r.NumeroMovimientos:N0} movimientos; acota el periodo o los filtros para ver el resto.")
                        .FontSize(8).FontColor(Rojo);
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(cd =>
                    {
                        cd.ConstantColumn(72); cd.RelativeColumn(2); cd.ConstantColumn(70); cd.RelativeColumn(4); cd.RelativeColumn(2.4f);
                        cd.ConstantColumn(62); cd.ConstantColumn(58); cd.ConstantColumn(62); cd.ConstantColumn(70); cd.ConstantColumn(55); cd.RelativeColumn(2);
                    });
                    t.Header(h =>
                    {
                        Th(h.Cell(), "Fecha"); Th(h.Cell(), "Almacén"); Th(h.Cell(), "Referencia"); Th(h.Cell(), "Descripción");
                        Th(h.Cell(), "Movimiento"); Th(h.Cell(), "Documento"); Th(h.Cell(), "Cantidad", derecha: true);
                        Th(h.Cell(), "Costo unit.", derecha: true); Th(h.Cell(), "Valor", derecha: true); Th(h.Cell(), "Saldo", derecha: true);
                        Th(h.Cell(), "Usuario");
                    });
                    foreach (var m in r.Movimientos.Elementos)
                    {
                        Td(t.Cell(), FechaHora(m.Fecha)); Td(t.Cell(), m.Almacen); Td(t.Cell(), m.Referencia, negrita: true);
                        Td(t.Cell(), m.Descripcion); Td(t.Cell(), Application.Movimientos.EtiquetasMovimiento.Nombre(m.Tipo));
                        Td(t.Cell(), m.DocumentoTipo is { } tipo ? $"{Application.Movimientos.EtiquetasMovimiento.Prefijo(tipo)}-{m.Folio:D5}" : "—");
                        Td(t.Cell(), (m.Cantidad > 0 ? "+" : "") + Cantidad(m.Cantidad), derecha: true);
                        Td(t.Cell(), Costo(m.CostoUnitario), derecha: true); Td(t.Cell(), Dinero(m.Valor), derecha: true);
                        Td(t.Cell(), Cantidad(m.Saldo), derecha: true); Td(t.Cell(), m.Usuario);
                    }
                });
            });

            page.Footer().Element(c => Pie(c, generadoEn, $"Generado por {generadoPor}. Cantidades positivas son entradas y negativas salidas."));
        })).GeneratePdf();

    // =============================================================== Piezas comunes

    private static void Pagina(PageDescriptor page)
    {
        page.Size(PageSizes.Letter);
        page.Margin(36);
        page.PageColor(Colors.White);
        page.DefaultTextStyle(x => x.FontSize(9).FontColor("#1B2733"));
    }

    private void Encabezado(IContainer c, string titulo, string? folio, string subtitulo) =>
        c.BorderBottom(2).BorderColor(Primario).PaddingBottom(8).Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text(_empresa.Nombre).FontSize(15).Bold().FontColor(Oscuro);
                if (_empresa.Rfc is { } rfc) col.Item().Text($"RFC: {rfc}").FontColor(Gris);
                if (_empresa.Direccion is { } dir) col.Item().Text(dir).FontColor(Gris);
                var contacto = string.Join(" · ", new[] { _empresa.Telefono, _empresa.Correo }.Where(x => !string.IsNullOrWhiteSpace(x)));
                if (contacto.Length > 0) col.Item().Text(contacto).FontColor(Gris);
            });
            row.AutoItem().AlignRight().Column(col =>
            {
                col.Item().AlignRight().Text(titulo.ToUpperInvariant()).FontSize(10).SemiBold().FontColor(Gris);
                if (folio is not null) col.Item().AlignRight().Text(folio).FontSize(18).Bold().FontColor(Primario);
                col.Item().AlignRight().Text(subtitulo);
            });
        });

    private static void Pie(IContainer c, DateTimeOffset generadoEn, string nota) =>
        c.BorderTop(1).BorderColor(Linea).PaddingTop(6).Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text(nota).FontSize(7).FontColor(Gris);
                col.Item().Text($"Generado el {FechaHora(generadoEn)}").FontSize(7).FontColor(Gris);
            });
            row.AutoItem().AlignRight().Text(t =>
            {
                t.DefaultTextStyle(x => x.FontSize(7).FontColor(Gris));
                t.Span("Página ");
                t.CurrentPageNumber();
                t.Span(" de ");
                t.TotalPages();
            });
        });

    private static IContainer Caja(IContainer c) => c.Background(Fondo).Border(1).BorderColor(Linea).Padding(8);

    private static void Dato(ColumnDescriptor c, string etiqueta, string valor) =>
        c.Item().Text(t =>
        {
            t.Span($"{etiqueta}: ").FontColor(Gris);
            t.Span(valor).SemiBold();
        });

    private static void Total(ColumnDescriptor c, string etiqueta, string valor) =>
        c.Item().Row(r =>
        {
            r.RelativeItem().Text(etiqueta).FontColor(Gris);
            r.AutoItem().Text(valor);
        });

    private static void Firma(IContainer c, string texto) =>
        c.Column(col =>
        {
            col.Item().Height(30);
            col.Item().BorderTop(1).BorderColor(Gris).PaddingTop(4).AlignCenter().Text(texto).FontColor(Gris);
        });

    private static void Th(IContainer c, string texto, bool derecha = false)
    {
        var celda = c.Background(Oscuro).PaddingVertical(5).PaddingHorizontal(4);
        (derecha ? celda.AlignRight() : celda).Text(texto).FontColor(Colors.White).SemiBold().FontSize(8);
    }

    private static void Td(IContainer c, string texto, bool derecha = false, bool negrita = false)
    {
        var celda = c.BorderBottom(1).BorderColor(Linea).PaddingVertical(4).PaddingHorizontal(4);
        var t = (derecha ? celda.AlignRight() : celda).Text(texto);
        if (negrita) t.SemiBold();
    }

    private static void Titulo(ColumnDescriptor col, string texto) =>
        col.Item().PaddingTop(4).BorderBottom(1).BorderColor(Linea).PaddingBottom(3).Text(texto).FontSize(13).Bold().FontColor(Oscuro);

    private static void Indicadores(ColumnDescriptor col, params (string Etiqueta, string Valor)[] datos) =>
        col.Item().Row(row =>
        {
            row.Spacing(8);
            foreach (var (etiqueta, valor) in datos)
                row.RelativeItem().Element(Caja).Column(c =>
                {
                    c.Item().Text(etiqueta).FontSize(7).FontColor(Gris);
                    c.Item().Text(valor).FontSize(12).Bold().FontColor(Primario);
                });
        });

    private static void Grafica(ColumnDescriptor col, string titulo, byte[] png) =>
        col.Item().ShowEntire().Column(c =>
        {
            c.Item().Text(titulo).SemiBold();
            c.Item().PaddingTop(4).Image(png).FitWidth();
        });

    private static void TablaTop(IContainer c, string titulo, IReadOnlyList<PuntoDto> puntos) =>
        c.Column(col =>
        {
            col.Item().PaddingBottom(4).Text(titulo).SemiBold();
            if (puntos.Count == 0)
            {
                col.Item().Text("Sin datos en el periodo.").FontColor(Gris);
                return;
            }
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(cd => { cd.ConstantColumn(18); cd.RelativeColumn(); cd.ConstantColumn(80); });
                var i = 0;
                foreach (var p in puntos)
                {
                    Td(t.Cell(), (++i).ToString());
                    Td(t.Cell(), p.Etiqueta);
                    Td(t.Cell(), Dinero(p.Valor), derecha: true);
                }
            });
        });

    private static string Dinero(decimal v) => v.ToString("C2", Mx);
    private static string Costo(decimal v) => v.ToString(Math.Round(v, 2) == v ? "C2" : "C4", Mx);
    private static string Cantidad(decimal v) => v.ToString(decimal.Truncate(v) == v ? "N0" : "N3", Mx);

    private static string FechaHora(DateTimeOffset? f) =>
        f is { } x ? TimeZoneInfo.ConvertTime(x, ZonaMexico).ToString("dd MMM yyyy HH:mm", Mx) : "—";
}
