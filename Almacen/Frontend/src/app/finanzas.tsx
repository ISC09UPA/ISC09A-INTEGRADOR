import { router } from 'expo-router';
import { useState } from 'react';
import { Pressable, View } from 'react-native';

import { archivosService, finanzasService } from '@/services';
import type { NaturalezaFinanciera } from '@/types/enums';
import { RangoFechas, type Rango } from '@/components/fechas';
import {
  Aviso,
  Boton,
  BotonIcono,
  Cargando,
  Chip,
  Encabezado,
  Fila,
  Indicador,
  Indicadores,
  Pantalla,
  Selector,
  Separador,
  Tarjeta,
  Texto,
  Vacio,
  avisar,
  mensajeDe,
} from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { useColores } from '@/hooks/use-colores';
import { useDatos } from '@/hooks/use-datos';
import { Fechas, Formato } from '@/lib/formato';
import { mostrarArchivo } from '@/lib/visor';

const TAMANO = 50;

/** Movimientos financieros y reporte mensual Debe/Haber (Finanzas.razor + ReporteMensualFinanzas.razor). */
export default function Finanzas() {
  const [vista, setVista] = useState<'movimientos' | 'mensual'>('movimientos');
  return (
    <Pantalla>
      <Encabezado titulo="Movimientos financieros" descripcion="Se generan solos: ingreso por cada venta, egreso por cada recepción y su reverso al cancelar." />
      <Fila style={{ marginBottom: Spacing.three }}>
        <Boton texto="Movimientos" variante={vista === 'movimientos' ? 'lleno' : 'contorno'} alPresionar={() => setVista('movimientos')} />
        <Boton texto="Reporte mensual" variante={vista === 'mensual' ? 'lleno' : 'contorno'} alPresionar={() => setVista('mensual')} />
      </Fila>
      {vista === 'movimientos' ? <Movimientos /> : <Mensual />}
    </Pantalla>
  );
}

function Movimientos() {
  const c = useColores();
  const [rango, setRango] = useState<Rango | null>(null);
  const [naturaleza, setNaturaleza] = useState<NaturalezaFinanciera | ''>('');
  const [paginas, setPaginas] = useState(1);
  const { datos, error, cargando } = useDatos(
    () =>
      finanzasService.movimientos({
        desde: rango?.desde,
        hasta: rango?.hasta,
        naturaleza: naturaleza || undefined,
        pagina: 1,
        tamano: TAMANO * paginas,
      }),
    [rango, naturaleza, paginas],
  );

  return (
    <View>
      <Tarjeta style={{ gap: Spacing.three, marginBottom: Spacing.three }}>
        <RangoFechas
          valor={rango}
          alCambiar={(r) => {
            setPaginas(1);
            setRango(r);
          }}
          opcional
        />
        <Selector<NaturalezaFinanciera | ''>
          etiqueta="Tipo"
          valor={naturaleza}
          alCambiar={(n) => {
            setPaginas(1);
            setNaturaleza(n);
          }}
          opciones={[
            { valor: '', texto: 'Todos' },
            { valor: 'Ingreso', texto: 'Ingresos (ventas)' },
            { valor: 'Egreso', texto: 'Egresos (compras)' },
          ]}
        />
      </Tarjeta>
      {error ? <Aviso tipo="error" texto={error} /> : null}
      {!datos && cargando ? <Cargando /> : null}
      {datos ? (
        <>
          <Indicadores>
            <Indicador titulo="Ingresos" valor={Formato.dinero(datos.ingresos)} color="success" />
            <Indicador titulo="Egresos" valor={Formato.dinero(datos.egresos)} color="error" />
            <Indicador titulo="Saldo" valor={Formato.dinero(datos.saldo)} color={datos.saldo >= 0 ? 'primary' : 'error'} />
            <Indicador titulo="IVA trasladado − acreditable" valor={Formato.dinero(datos.ivaPorPagar)} />
          </Indicadores>
          {datos.movimientos.length === 0 ? (
            <Vacio texto="No hay movimientos en el periodo." />
          ) : (
            <Tarjeta style={{ gap: Spacing.two }}>
              {datos.movimientos.map((m, i) => (
                <View key={m.id} style={{ gap: 2 }}>
                  {i > 0 ? <Separador /> : null}
                  <Fila style={{ justifyContent: 'space-between' }}>
                    <Fila gap={6}>
                      <Chip texto={m.naturaleza} color={m.naturaleza === 'Ingreso' ? 'success' : 'error'} />
                      {m.esReverso ? <Chip texto="Reverso" color="warning" /> : null}
                    </Fila>
                    <Texto tipo="fuerte" color={m.total >= 0 ? undefined : c.error}>
                      {Formato.dinero(m.total)}
                    </Texto>
                  </Fila>
                  <Texto numberOfLines={1}>{m.tercero}</Texto>
                  <Fila style={{ justifyContent: 'space-between', flexWrap: 'wrap' }}>
                    <Texto tipo="pequeno">{Formato.fechaHora(m.fecha)}</Texto>
                    <Pressable onPress={() => router.push(Formato.ruta(m.documento.tipo, m.documento.id) as never)}>
                      <Texto tipo="pequeno" color={c.primary}>
                        {Formato.folio(Formato.prefijo(m.documento.tipo), m.documento.folio)}
                      </Texto>
                    </Pressable>
                  </Fila>
                  <Texto tipo="pequeno">
                    Subtotal {Formato.dinero(m.subtotal)} · IVA {Formato.dinero(m.iva)}
                  </Texto>
                </View>
              ))}
              {datos.movimientos.length < datos.total ? <Boton texto="Cargar más" variante="texto" cargando={cargando} alPresionar={() => setPaginas((p) => p + 1)} /> : null}
            </Tarjeta>
          )}
        </>
      ) : null}
    </View>
  );
}

function Mensual() {
  const c = useColores();
  const [anio, setAnio] = useState(Number(Fechas.hoy().slice(0, 4)));
  const [abriendo, setAbriendo] = useState(false);
  const { datos: r, error, cargando } = useDatos(() => finanzasService.reporteMensual(anio), [anio]);

  return (
    <View style={{ gap: Spacing.three }}>
      <Texto tipo="secundario">Moneda: MXN · Debe = egresos (compras) · Haber = ingresos (ventas) · los reversos por cancelación restan.</Texto>
      <Fila style={{ justifyContent: 'space-between', flexWrap: 'wrap' }}>
        <Fila>
          <BotonIcono icono="chevron-left" etiqueta="Año anterior" alPresionar={() => setAnio(anio - 1)} />
          <Texto tipo="fuerte">Año {anio}</Texto>
          <BotonIcono icono="chevron-right" etiqueta="Año siguiente" alPresionar={() => setAnio(anio + 1)} />
        </Fila>
        <Boton
          texto="Ver PDF"
          icono="picture-as-pdf"
          variante="contorno"
          cargando={abriendo}
          alPresionar={async () => {
            setAbriendo(true);
            try {
              await mostrarArchivo(await archivosService.reporteMensual(anio));
            } catch (e) {
              avisar('No se pudo abrir el PDF', mensajeDe(e));
            } finally {
              setAbriendo(false);
            }
          }}
        />
      </Fila>
      {error ? <Aviso tipo="error" texto={error} /> : null}
      {!r && cargando ? <Cargando /> : null}
      {r ? (
        <>
          <Indicadores>
            <Indicador titulo="Saldo actual" valor={Formato.dinero(r.saldoActual)} color={r.saldoActual >= 0 ? 'primary' : 'error'} detalle="Histórico: ingresos − egresos" />
          </Indicadores>
          {/* Un bloque por año: en teléfono es más legible que la tabla ancha de la web. */}
          {[...r.anios].reverse().map((a) => {
            const total = r.totales.find((t) => t.anio === a);
            return (
              <Tarjeta key={a} style={{ gap: Spacing.two }}>
                <Fila style={{ justifyContent: 'space-between' }}>
                  <Texto tipo="subtitulo">{a}</Texto>
                  <Texto tipo="pequeno">Debe / Haber</Texto>
                </Fila>
                {r.meses.map((mes) => {
                  const val = mes.porAnio.find((x) => x.anio === a);
                  return (
                    <Fila key={mes.mes} style={{ justifyContent: 'space-between' }}>
                      <Texto tipo="secundario" style={{ width: 90 }}>
                        {mes.nombre}
                      </Texto>
                      <Texto color={c.error} style={{ flex: 1, textAlign: 'right' }}>
                        {Formato.dinero(val?.debe ?? 0)}
                      </Texto>
                      <Texto color={c.success} style={{ flex: 1, textAlign: 'right' }}>
                        {Formato.dinero(val?.haber ?? 0)}
                      </Texto>
                    </Fila>
                  );
                })}
                <Separador />
                <Fila style={{ justifyContent: 'space-between' }}>
                  <Texto tipo="fuerte" style={{ width: 90 }}>
                    Total
                  </Texto>
                  <Texto tipo="fuerte" color={c.error} style={{ flex: 1, textAlign: 'right' }}>
                    {Formato.dinero(total?.debe ?? 0)}
                  </Texto>
                  <Texto tipo="fuerte" color={c.success} style={{ flex: 1, textAlign: 'right' }}>
                    {Formato.dinero(total?.haber ?? 0)}
                  </Texto>
                </Fila>
              </Tarjeta>
            );
          })}
        </>
      ) : null}
    </View>
  );
}
