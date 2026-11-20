import { router, useLocalSearchParams } from 'expo-router';
import { useEffect, useState } from 'react';
import { Pressable, View } from 'react-native';

import { ProductoMapper } from '@/mappers/producto.mapper';
import type { MovimientoKardex } from '@/models/movimiento-kardex.model';
import type { ProductoOpcion } from '@/models/producto-opcion.model';
import { almacenesService, productosService } from '@/services';
import { RangoFechas, type Rango } from '@/components/fechas';
import { SelectorProducto } from '@/components/selectores';
import { Aviso, Boton, Cargando, Encabezado, Fila, Pantalla, Selector, Separador, Tarjeta, Texto, Vacio, mensajeDe } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { useColores } from '@/hooks/use-colores';
import { useDatos } from '@/hooks/use-datos';
import { Formato } from '@/lib/formato';

/** Historial de un producto en un almacén (Kardex.razor). Acepta ?almacen=&producto= desde Existencias. */
export default function KardexPantalla() {
  const c = useColores();
  const params = useLocalSearchParams<{ almacen?: string; producto?: string }>();
  const [almacenId, setAlmacenId] = useState<string | undefined>(params.almacen);
  const [producto, setProducto] = useState<ProductoOpcion | null>(null);
  const [rango, setRango] = useState<Rango | null>(null);
  const [filas, setFilas] = useState<MovimientoKardex[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [cargando, setCargando] = useState(false);
  const almacenes = useDatos(() => almacenesService.listar());

  const consultar = async (alm = almacenId, prod = producto) => {
    if (!alm || !prod) return;
    setCargando(true);
    setError(null);
    try {
      setFilas(await almacenesService.kardex(alm, prod.id, rango?.desde, rango?.hasta));
    } catch (e) {
      setError(mensajeDe(e));
    } finally {
      setCargando(false);
    }
  };

  // Al llegar desde Existencias se precarga el producto y se consulta de una vez.
  useEffect(() => {
    if (!params.producto) return;
    productosService
      .obtener(params.producto)
      .then((p) => {
        const opcion = ProductoMapper.comoOpcion(p);
        setProducto(opcion);
        void consultar(params.almacen, opcion);
      })
      .catch((e) => setError(mensajeDe(e)));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [params.producto, params.almacen]);

  return (
    <Pantalla>
      <Encabezado titulo="Kardex" descripcion="Historial de entradas y salidas de un producto en un almacén, con el saldo y el costo promedio después de cada movimiento." />
      <Tarjeta style={{ gap: Spacing.three, marginBottom: Spacing.three }}>
        <Selector<string> etiqueta="Almacén" valor={almacenId} opciones={(almacenes.datos ?? []).map((a) => ({ valor: a.id, texto: a.nombre }))} alCambiar={setAlmacenId} />
        <SelectorProducto valor={producto} alCambiar={setProducto} />
        <Texto tipo="secundario">Periodo (opcional)</Texto>
        <RangoFechas valor={rango} alCambiar={setRango} opcional />
        <Boton texto="Consultar" alPresionar={() => consultar()} deshabilitado={!almacenId || !producto} cargando={cargando} />
      </Tarjeta>

      {error ? <Aviso tipo="error" texto={error} /> : null}
      {cargando && !filas ? <Cargando /> : null}
      {filas && filas.length === 0 ? <Vacio texto="Sin movimientos en el periodo." /> : null}
      {filas && filas.length > 0 ? (
        <Tarjeta style={{ gap: Spacing.two }}>
          {filas.map((k, i) => (
            <View key={i} style={{ gap: 2 }}>
              {i > 0 ? <Separador /> : null}
              <Fila style={{ justifyContent: 'space-between' }}>
                <Texto tipo="fuerte">{Formato.movimiento(k.tipo)}</Texto>
                <Texto tipo="fuerte" color={k.cantidad >= 0 ? c.success : c.error}>
                  {k.cantidad >= 0 ? '+' : '−'}
                  {Formato.cantidad(Math.abs(k.cantidad))}
                </Texto>
              </Fila>
              <Fila style={{ justifyContent: 'space-between', flexWrap: 'wrap' }}>
                <Texto tipo="pequeno">
                  {Formato.fechaHora(k.fecha)} · {k.usuario}
                </Texto>
                {k.documento ? (
                  <Pressable onPress={() => router.push(Formato.ruta(k.documento!.tipo, k.documento!.id) as never)}>
                    <Texto tipo="pequeno" color={c.primary}>
                      {Formato.folio(Formato.prefijo(k.documento.tipo), k.documento.folio)}
                    </Texto>
                  </Pressable>
                ) : null}
              </Fila>
              <Fila style={{ justifyContent: 'space-between', flexWrap: 'wrap' }}>
                <Texto tipo="secundario">Costo {Formato.costo(k.costoUnitario)} · prom. {Formato.costo(k.costoPromedio)}</Texto>
                <Texto>Saldo {Formato.cantidad(k.saldo)}</Texto>
              </Fila>
            </View>
          ))}
        </Tarjeta>
      ) : null}
    </Pantalla>
  );
}
