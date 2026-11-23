import { router } from 'expo-router';
import { useState } from 'react';
import { Pressable, View } from 'react-native';

import { almacenesService, archivosService, movimientosService } from '@/services';
import type { FiltroMovimientos } from '@/services/movimientos.service';
import type { TipoMovimientoInventario } from '@/types/enums';
import { RangoFechas, rangoMes, type Rango } from '@/components/fechas';
import {
  Aviso,
  Boton,
  Busqueda,
  Cargando,
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
import { Formato } from '@/lib/formato';
import { mostrarArchivo } from '@/lib/visor';

const TIPOS: TipoMovimientoInventario[] = ['Inicial', 'Recepcion', 'Venta', 'TraspasoSalida', 'TraspasoEntrada', 'Ajuste', 'CancelacionRecepcion', 'CancelacionVenta'];
const TAMANO = 30;

/** Movimientos de inventario con totales, resumen por producto, PDF y CSV (Movimientos.razor). */
export default function Movimientos() {
  const c = useColores();
  const [rango, setRango] = useState<Rango>(rangoMes());
  const [almacen, setAlmacen] = useState('');
  const [tipo, setTipo] = useState<TipoMovimientoInventario | ''>('');
  const [busqueda, setBusqueda] = useState('');
  const [paginas, setPaginas] = useState(1);
  const [vista, setVista] = useState<'movimientos' | 'resumen'>('movimientos');
  const [exportando, setExportando] = useState<string | null>(null);

  const filtro: FiltroMovimientos = { desde: rango.desde, hasta: rango.hasta, almacenId: almacen || undefined, tipo: tipo || undefined, busqueda: busqueda || undefined };
  const almacenes = useDatos(() => almacenesService.listar());
  const { datos, error, cargando, recargar } = useDatos(
    () => movimientosService.reporte(filtro, 1, TAMANO * paginas),
    [filtro, paginas],
  );

  const exportar = async (formato: 'pdf' | 'csv') => {
    setExportando(formato);
    try {
      await mostrarArchivo(formato === 'pdf' ? await archivosService.movimientosPdf(filtro) : await archivosService.movimientosCsv(filtro));
    } catch (e) {
      avisar('No se pudo exportar', mensajeDe(e));
    } finally {
      setExportando(null);
    }
  };

  const reiniciar = <T,>(fijar: (v: T) => void) => (v: T) => {
    setPaginas(1);
    fijar(v);
  };

  return (
    <Pantalla alRecargar={recargar} recargando={cargando && !!datos}>
      <Encabezado
        titulo="Movimientos de inventario"
        descripcion="Todas las entradas y salidas de todos los productos y almacenes, con totales y resumen por producto."
        acciones={
          <>
            <Boton texto="Ver PDF" icono="picture-as-pdf" variante="contorno" cargando={exportando === 'pdf'} alPresionar={() => exportar('pdf')} />
            <Boton texto="Exportar CSV" icono="table-view" variante="contorno" cargando={exportando === 'csv'} alPresionar={() => exportar('csv')} />
          </>
        }
      />
      <Tarjeta style={{ gap: Spacing.three, marginBottom: Spacing.three }}>
        <RangoFechas valor={rango} alCambiar={(r) => r && reiniciar(setRango)(r)} />
        <Selector<string>
          etiqueta="Almacén"
          valor={almacen}
          opciones={[{ valor: '', texto: 'Todos' }, ...(almacenes.datos ?? []).map((a) => ({ valor: a.id, texto: a.nombre }))]}
          alCambiar={reiniciar(setAlmacen)}
        />
        <Selector<TipoMovimientoInventario | ''>
          etiqueta="Tipo de movimiento"
          valor={tipo}
          opciones={[{ valor: '', texto: 'Todos' }, ...TIPOS.map((t) => ({ valor: t, texto: Formato.movimiento(t) }))]}
          alCambiar={reiniciar(setTipo)}
        />
        <Busqueda valor={busqueda} alCambiar={reiniciar(setBusqueda)} placeholder="Referencia o descripción" />
      </Tarjeta>

      {error ? <Aviso tipo="error" texto={error} /> : null}
      {!datos && cargando ? <Cargando /> : null}
      {datos ? (
        <>
          <Indicadores>
            <Indicador titulo="Movimientos" valor={Formato.numero(datos.numeroMovimientos)} />
            <Indicador titulo="Entradas" valor={Formato.cantidad(datos.entradas)} detalle={Formato.dinero(datos.valorEntradas)} color="success" />
            <Indicador titulo="Salidas" valor={Formato.cantidad(datos.salidas)} detalle={Formato.dinero(datos.valorSalidas)} color="error" />
          </Indicadores>

          <Fila style={{ marginBottom: Spacing.two }}>
            <Boton texto="Movimientos" variante={vista === 'movimientos' ? 'lleno' : 'contorno'} alPresionar={() => setVista('movimientos')} />
            <Boton texto="Resumen por producto" variante={vista === 'resumen' ? 'lleno' : 'contorno'} alPresionar={() => setVista('resumen')} />
          </Fila>

          {vista === 'movimientos' ? (
            datos.movimientos.elementos.length === 0 ? (
              <Vacio texto="No hay movimientos con estos filtros." />
            ) : (
              <Tarjeta style={{ gap: Spacing.two }}>
                {datos.movimientos.elementos.map((m, i) => (
                  <View key={m.id} style={{ gap: 2 }}>
                    {i > 0 ? <Separador /> : null}
                    <Fila style={{ justifyContent: 'space-between' }}>
                      <Texto tipo="fuerte" style={{ flex: 1 }} numberOfLines={1}>
                        {m.referencia} <Texto tipo="secundario">{m.descripcion}</Texto>
                      </Texto>
                      <Texto tipo="fuerte" color={m.cantidad >= 0 ? c.success : c.error}>
                        {m.cantidad >= 0 ? '+' : '−'}
                        {Formato.cantidad(Math.abs(m.cantidad))} {m.unidad}
                      </Texto>
                    </Fila>
                    <Fila style={{ justifyContent: 'space-between', flexWrap: 'wrap' }}>
                      <Texto tipo="pequeno">
                        {Formato.movimiento(m.tipo)} · {m.almacen} · {Formato.fechaHora(m.fecha)}
                      </Texto>
                      {m.documento ? (
                        <Pressable onPress={() => router.push(Formato.ruta(m.documento!.tipo, m.documento!.id) as never)}>
                          <Texto tipo="pequeno" color={c.primary}>
                            {Formato.folio(Formato.prefijo(m.documento.tipo), m.documento.folio)}
                          </Texto>
                        </Pressable>
                      ) : null}
                    </Fila>
                    <Texto tipo="pequeno">
                      {Formato.costo(m.costoUnitario)} · valor {Formato.dinero(m.valor)} · saldo {Formato.cantidad(m.saldo)} · {m.usuario}
                    </Texto>
                  </View>
                ))}
                {datos.movimientos.elementos.length < datos.movimientos.total ? (
                  <Boton texto="Cargar más" variante="texto" cargando={cargando} alPresionar={() => setPaginas((p) => p + 1)} />
                ) : null}
              </Tarjeta>
            )
          ) : datos.porProducto.length === 0 ? (
            <Vacio texto="Sin productos en el periodo." />
          ) : (
            <Tarjeta style={{ gap: Spacing.two }}>
              {datos.porProducto.map((r, i) => (
                <View key={`${r.almacen}-${r.referencia}`} style={{ gap: 2 }}>
                  {i > 0 ? <Separador /> : null}
                  <Texto tipo="fuerte">
                    {r.referencia} <Texto tipo="secundario">{r.descripcion}</Texto>
                  </Texto>
                  <Texto tipo="pequeno">{r.almacen}</Texto>
                  <Fila style={{ justifyContent: 'space-between', flexWrap: 'wrap' }}>
                    <Texto tipo="secundario">Inicial {Formato.cantidad(r.inicial)}</Texto>
                    <Texto color={c.success}>+{Formato.cantidad(r.entradas)}</Texto>
                    <Texto color={c.error}>−{Formato.cantidad(r.salidas)}</Texto>
                    <Texto tipo="fuerte">Final {Formato.cantidad(r.final)} {r.unidad}</Texto>
                  </Fila>
                </View>
              ))}
            </Tarjeta>
          )}
        </>
      ) : null}
    </Pantalla>
  );
}
