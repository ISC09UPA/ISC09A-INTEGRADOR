import { useState } from 'react';
import { Pressable, ScrollView, Text, View } from 'react-native';

import { archivosService, reportesService } from '@/services';
import type { TipoReporte } from '@/types/enums';
import { RangoFechas, rangoMes, type Rango } from '@/components/fechas';
import { Barras, BarrasDobles } from '@/components/graficas';
import { Aviso, Boton, Cargando, Encabezado, Fila, Indicador, Indicadores, Pantalla, Separador, Tarjeta, Texto, Vacio, avisar, mensajeDe } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { useColores } from '@/hooks/use-colores';
import { useDatos } from '@/hooks/use-datos';
import { Formato } from '@/lib/formato';
import { mostrarArchivo } from '@/lib/visor';

const PESTANAS: { tipo: Exclude<TipoReporte, 'General'>; texto: string }[] = [
  { tipo: 'Ventas', texto: 'Ventas' },
  { tipo: 'Compras', texto: 'Compras' },
  { tipo: 'Inventario', texto: 'Inventario' },
  { tipo: 'Financiero', texto: 'Flujo financiero' },
  { tipo: 'Margen', texto: 'Margen' },
];

/** Reportes con indicadores, gráficas y PDF por pestaña o general (Reportes.razor). */
export default function Reportes() {
  const c = useColores();
  const [rango, setRango] = useState<Rango>(rangoMes());
  const [pestana, setPestana] = useState<(typeof PESTANAS)[number]['tipo']>('Ventas');
  const [abriendo, setAbriendo] = useState<TipoReporte | null>(null);

  const { datos, error, cargando, recargar } = useDatos(async () => {
    const p = { desde: rango.desde, hasta: rango.hasta };
    switch (pestana) {
      case 'Ventas':
        return { ventas: await reportesService.ventas(p) };
      case 'Compras':
        return { compras: await reportesService.compras(p) };
      case 'Inventario':
        return { inventario: await reportesService.inventario() };
      case 'Financiero':
        return { financiero: await reportesService.financiero(p) };
      case 'Margen':
        return { margen: await reportesService.margen(p) };
    }
  }, [rango, pestana]);

  const pdf = async (tipo: TipoReporte) => {
    setAbriendo(tipo);
    try {
      await mostrarArchivo(await archivosService.reporte(tipo, rango));
    } catch (e) {
      avisar('No se pudo abrir el PDF', mensajeDe(e));
    } finally {
      setAbriendo(null);
    }
  };

  const nombre = PESTANAS.find((p) => p.tipo === pestana)!.texto;
  const v = datos?.ventas;
  const co = datos?.compras;
  const inv = datos?.inventario;
  const f = datos?.financiero;
  const m = datos?.margen;

  return (
    <Pantalla alRecargar={recargar} recargando={cargando && !!datos}>
      <Encabezado
        titulo="Reportes"
        descripcion="Ventas, compras, inventario, flujo financiero y margen. Las ventas y recepciones canceladas no cuentan."
        acciones={
          <>
            <Boton texto={`PDF: ${nombre}`} icono="picture-as-pdf" variante="contorno" cargando={abriendo === pestana} alPresionar={() => pdf(pestana)} />
            <Boton texto="PDF general" icono="picture-as-pdf" variante="contorno" cargando={abriendo === 'General'} alPresionar={() => pdf('General')} />
          </>
        }
      />
      <Tarjeta style={{ marginBottom: Spacing.three }}>
        <RangoFechas valor={rango} alCambiar={(r) => r && setRango(r)} />
      </Tarjeta>

      <ScrollView horizontal showsHorizontalScrollIndicator={false} style={{ marginBottom: Spacing.three }} contentContainerStyle={{ gap: 6 }}>
        {PESTANAS.map((p) => {
          const activa = p.tipo === pestana;
          return (
            <Pressable
              key={p.tipo}
              onPress={() => setPestana(p.tipo)}
              style={{ paddingHorizontal: 14, paddingVertical: 8, borderRadius: 999, borderWidth: 1, borderColor: activa ? c.primary : c.line, backgroundColor: activa ? c.primary : c.surface }}>
              <Text style={{ color: activa ? c.onPrimary : c.text, fontWeight: '600' }}>{p.texto}</Text>
            </Pressable>
          );
        })}
      </ScrollView>

      {error ? <Aviso tipo="error" texto={error} /> : null}
      {cargando && !datos ? <Cargando /> : null}

      {pestana === 'Ventas' && v ? (
        <>
          <Indicadores>
            <Indicador titulo="Total vendido" valor={Formato.dinero(v.total)} color="primary" detalle={`IVA ${Formato.dinero(v.iva)}`} />
            <Indicador titulo="Ventas" valor={Formato.numero(v.numeroVentas)} />
            <Indicador titulo="Ticket promedio" valor={Formato.dinero(v.ticketPromedio)} />
            <Indicador titulo="Subtotal" valor={Formato.dinero(v.subtotal)} />
          </Indicadores>
          <Barras titulo="Ventas por periodo" puntos={v.porPeriodo} />
          <Barras titulo="Top 10 clientes (total)" puntos={v.topClientes} />
          <Barras titulo="Top 10 productos (subtotal)" puntos={v.topProductos} />
          <Barras titulo="Ventas por almacén" puntos={v.porAlmacen} />
        </>
      ) : null}

      {pestana === 'Compras' && co ? (
        <>
          <Indicadores>
            <Indicador titulo="Total comprado" valor={Formato.dinero(co.total)} color="primary" detalle={`IVA ${Formato.dinero(co.iva)}`} />
            <Indicador titulo="Recepciones" valor={Formato.numero(co.numeroRecepciones)} />
            <Indicador titulo="Subtotal" valor={Formato.dinero(co.subtotal)} />
          </Indicadores>
          <Barras titulo="Compras por periodo" puntos={co.porPeriodo} />
          <Barras titulo="Compras por proveedor" puntos={co.porProveedor} />
        </>
      ) : null}

      {pestana === 'Inventario' && inv ? (
        <>
          <Indicadores>
            <Indicador titulo="Valor del inventario" valor={Formato.dinero(inv.valorTotal)} color="primary" detalle="A costo promedio, hoy" />
            <Indicador titulo="Productos bajo mínimo" valor={String(inv.bajoMinimo.length)} color={inv.bajoMinimo.length > 0 ? 'warning' : undefined} />
          </Indicadores>
          <Barras titulo="Valor por almacén" puntos={inv.valorPorAlmacen} />
          <Tarjeta style={{ gap: Spacing.two }}>
            <Texto tipo="fuerte">Bajo mínimo</Texto>
            {inv.bajoMinimo.length === 0 ? <Texto tipo="secundario">Ningún producto está en o por debajo de su mínimo.</Texto> : null}
            {inv.bajoMinimo.map((e, i) => (
              <View key={`${e.almacenId}-${e.productoId}`} style={{ gap: 2 }}>
                {i > 0 ? <Separador /> : null}
                <Texto tipo="fuerte">
                  {e.referencia} <Texto tipo="secundario">{e.descripcion}</Texto>
                </Texto>
                <Fila style={{ justifyContent: 'space-between' }}>
                  <Texto tipo="pequeno">{e.almacen}</Texto>
                  <Texto color={c.warning}>
                    {Formato.cantidad(e.cantidad)} / mín. {Formato.cantidad(e.stockMinimo)}
                  </Texto>
                </Fila>
              </View>
            ))}
          </Tarjeta>
        </>
      ) : null}

      {pestana === 'Financiero' && f ? (
        <>
          <Indicadores>
            <Indicador titulo="Ingresos" valor={Formato.dinero(f.totalIngresos)} color="success" />
            <Indicador titulo="Egresos" valor={Formato.dinero(f.totalEgresos)} color="error" />
            <Indicador titulo="Saldo" valor={Formato.dinero(f.saldo)} color={f.saldo >= 0 ? 'primary' : 'error'} />
            <Indicador
              titulo="IVA por pagar"
              valor={Formato.dinero(f.ivaPorPagar)}
              detalle={`Trasladado ${Formato.dinero(f.ivaTrasladado)} · acreditable ${Formato.dinero(f.ivaAcreditable)}`}
            />
          </Indicadores>
          <BarrasDobles titulo="Compras vs. ventas" periodos={f.periodos} a={f.ingresos} b={f.egresos} nombreA="Ingresos (ventas)" nombreB="Egresos (compras)" />
        </>
      ) : null}

      {pestana === 'Margen' && m ? (
        m.length === 0 ? (
          <Vacio texto="Sin ventas en el periodo." />
        ) : (
          <Tarjeta style={{ gap: Spacing.two }}>
            {m.map((r, i) => (
              <View key={r.referencia} style={{ gap: 2 }}>
                {i > 0 ? <Separador /> : null}
                <Fila style={{ justifyContent: 'space-between' }}>
                  <Texto tipo="fuerte" style={{ flex: 1 }} numberOfLines={1}>
                    {r.referencia} <Texto tipo="secundario">{r.descripcion}</Texto>
                  </Texto>
                  <Texto tipo="fuerte" color={r.margen >= 0 ? c.success : c.error}>
                    {r.margenPorcentaje} %
                  </Texto>
                </Fila>
                <Texto tipo="pequeno">
                  {Formato.cantidad(r.cantidad)} vendidos · ventas {Formato.dinero(r.ventas)} · costo {Formato.dinero(r.costo)} · margen {Formato.dinero(r.margen)}
                </Texto>
              </View>
            ))}
          </Tarjeta>
        )
      ) : null}
    </Pantalla>
  );
}
