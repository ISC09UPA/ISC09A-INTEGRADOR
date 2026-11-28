import { router, useLocalSearchParams } from 'expo-router';
import { useState } from 'react';
import { View } from 'react-native';

import type { Almacen } from '@/models/almacen.model';
import type { Existencia } from '@/models/existencia.model';
import type { ProductoOpcion } from '@/models/producto-opcion.model';
import { almacenesService } from '@/services';
import { ListaPantalla } from '@/components/lista';
import { SelectorProducto } from '@/components/selectores';
import {
  Aviso,
  Boton,
  BotonIcono,
  Busqueda,
  Campo,
  Chip,
  Fila,
  Hoja,
  Interruptor,
  Selector,
  Tarjeta,
  Texto,
  avisar,
  mensajeDe,
} from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { useColores } from '@/hooks/use-colores';
import { useDatos, usePaginado } from '@/hooks/use-datos';
import { Formato, leerNumero } from '@/lib/formato';
import { Permisos } from '@/lib/permisos';
import { useSesion } from '@/lib/sesion';

/** Existencias por almacén con stock mínimo y stock inicial (Existencias.razor). */
export default function Existencias() {
  const c = useColores();
  const { puede } = useSesion();
  const params = useLocalSearchParams<{ busqueda?: string }>();
  const [almacenId, setAlmacenId] = useState<string>('');
  const [busqueda, setBusqueda] = useState(params.busqueda ?? '');
  const [soloBajo, setSoloBajo] = useState(false);
  const [minimoDe, setMinimoDe] = useState<Existencia | null>(null);
  const [inicial, setInicial] = useState(false);
  const gestionar = puede(Permisos.Almacenes.Gestionar);

  const almacenes = useDatos(() => almacenesService.listar());
  const lista = usePaginado<Existencia>(
    (pagina) =>
      almacenesService.existencias({ almacenId: almacenId || undefined, busqueda, soloBajoMinimo: soloBajo, pagina }),
    [almacenId, busqueda, soloBajo],
  );

  return (
    <>
      <ListaPantalla
        {...lista}
        alRecargar={lista.recargar}
        alLlegarAlFinal={lista.cargarMas}
        clave={(e) => `${e.almacenId}-${e.productoId}`}
        vacio="No hay existencias que coincidan. Se crean al recibir mercancía o al capturar stock inicial."
        encabezado={
          <View style={{ gap: Spacing.two }}>
            <Texto tipo="secundario">Lo que hay de cada producto en cada almacén, con su costo promedio ponderado.</Texto>
            {gestionar ? <Boton texto="Stock inicial" icono="add" alPresionar={() => setInicial(true)} style={{ alignSelf: 'flex-start' }} /> : null}
            <Busqueda valor={busqueda} alCambiar={setBusqueda} placeholder="Referencia o descripción" />
            <Selector<string>
              etiqueta="Almacén"
              valor={almacenId}
              opciones={[{ valor: '', texto: 'Todos los almacenes' }, ...(almacenes.datos ?? []).map((a) => ({ valor: a.id, texto: a.nombre }))]}
              alCambiar={setAlmacenId}
            />
            <Interruptor etiqueta="Solo bajo mínimo" valor={soloBajo} alCambiar={setSoloBajo} />
          </View>
        }
        fila={(e) => (
          <Tarjeta style={{ gap: 6 }}>
            <Fila style={{ justifyContent: 'space-between' }}>
              <Texto tipo="fuerte" style={{ flex: 1 }} numberOfLines={1}>
                {e.referencia} <Texto tipo="secundario">{e.descripcion}</Texto>
              </Texto>
              {e.bajoMinimo ? <Chip texto="Bajo mínimo" color="warning" /> : null}
            </Fila>
            <Texto tipo="pequeno">{e.almacen}</Texto>
            <Fila style={{ justifyContent: 'space-between', flexWrap: 'wrap' }}>
              <Texto tipo="subtitulo" color={e.bajoMinimo ? c.warning : undefined}>
                {Formato.cantidad(e.cantidad)} {e.unidad}
              </Texto>
              <Texto tipo="secundario">Mínimo {e.stockMinimo > 0 ? Formato.cantidad(e.stockMinimo) : '—'}</Texto>
            </Fila>
            <Fila style={{ justifyContent: 'space-between', flexWrap: 'wrap' }}>
              <Texto tipo="pequeno">Costo prom. {Formato.costo(e.costoPromedio)}</Texto>
              <Texto tipo="fuerte">{Formato.dinero(e.valor)}</Texto>
            </Fila>
            <Fila style={{ justifyContent: 'flex-end' }}>
              <BotonIcono icono="receipt-long" etiqueta="Kardex" alPresionar={() => router.push(`/kardex?almacen=${e.almacenId}&producto=${e.productoId}`)} color={c.primary} />
              {gestionar ? <BotonIcono icono="tune" etiqueta="Definir mínimo" alPresionar={() => setMinimoDe(e)} color={c.primary} /> : null}
            </Fila>
          </Tarjeta>
        )}
      />
      <DialogoMinimo existencia={minimoDe} alCerrar={() => setMinimoDe(null)} alGuardar={lista.recargar} />
      <DialogoInicial visible={inicial} almacenes={almacenes.datos ?? []} alCerrar={() => setInicial(false)} alGuardar={lista.recargar} />
    </>
  );
}

function DialogoMinimo({ existencia, alCerrar, alGuardar }: { existencia: Existencia | null; alCerrar: () => void; alGuardar: () => void }) {
  const [minimo, setMinimo] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [guardando, setGuardando] = useState(false);
  const [para, setPara] = useState<Existencia | null>(null);
  if (existencia !== para) {
    setPara(existencia);
    setMinimo(existencia ? String(existencia.stockMinimo) : '');
    setError(null);
  }

  return (
    <Hoja visible={!!existencia} titulo="Stock mínimo" alCerrar={alCerrar}>
      {existencia ? (
        <View style={{ gap: Spacing.three }}>
          <Texto>
            <Texto tipo="fuerte">{existencia.referencia}</Texto> en {existencia.almacen}. Al llegar a este nivel se avisa por correo al encargado y al gerente.
          </Texto>
          <Campo etiqueta="Mínimo" value={minimo} onChangeText={setMinimo} keyboardType="decimal-pad" ayuda="0 para no avisar" />
          {error ? <Aviso tipo="error" texto={error} /> : null}
          <Fila style={{ justifyContent: 'flex-end' }}>
            <Boton texto="Cancelar" variante="texto" alPresionar={alCerrar} />
            <Boton
              texto="Guardar"
              cargando={guardando}
              alPresionar={async () => {
                const n = leerNumero(minimo);
                if (Number.isNaN(n) || n < 0) return setError('Escribe un número mayor o igual a cero.');
                setGuardando(true);
                try {
                  await almacenesService.definirMinimo(existencia.almacenId, existencia.productoId, n);
                  alCerrar();
                  alGuardar();
                } catch (e) {
                  setError(mensajeDe(e));
                } finally {
                  setGuardando(false);
                }
              }}
            />
          </Fila>
        </View>
      ) : null}
    </Hoja>
  );
}

function DialogoInicial({ visible, almacenes, alCerrar, alGuardar }: { visible: boolean; almacenes: Almacen[]; alCerrar: () => void; alGuardar: () => void }) {
  const [almacenId, setAlmacenId] = useState<string | undefined>();
  const [producto, setProducto] = useState<ProductoOpcion | null>(null);
  const [cantidad, setCantidad] = useState('');
  const [costo, setCosto] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [guardando, setGuardando] = useState(false);

  const cerrar = () => {
    setProducto(null);
    setCantidad('');
    setCosto('');
    setError(null);
    alCerrar();
  };

  return (
    <Hoja visible={visible} titulo="Stock inicial" alCerrar={cerrar}>
      <View style={{ gap: Spacing.three }}>
        <Texto tipo="secundario">Solo para productos que aún no tienen movimientos en el almacén elegido.</Texto>
        <Selector<string> etiqueta="Almacén" valor={almacenId} opciones={almacenes.map((a) => ({ valor: a.id, texto: a.nombre }))} alCambiar={setAlmacenId} />
        <SelectorProducto valor={producto} alCambiar={setProducto} />
        <Fila>
          <Campo etiqueta="Cantidad" value={cantidad} onChangeText={setCantidad} keyboardType="decimal-pad" style={{ flex: 1 }} />
          <Campo etiqueta="Costo unitario" value={costo} onChangeText={setCosto} keyboardType="decimal-pad" placeholder="$0.00" style={{ flex: 1 }} />
        </Fila>
        {error ? <Aviso tipo="error" texto={error} /> : null}
        <Fila style={{ justifyContent: 'flex-end' }}>
          <Boton texto="Cancelar" variante="texto" alPresionar={cerrar} />
          <Boton
            texto="Registrar"
            cargando={guardando}
            alPresionar={async () => {
              const n = leerNumero(cantidad);
              const k = leerNumero(costo);
              if (!almacenId || !producto) return setError('Elige almacén y producto.');
              if (!(n > 0) || Number.isNaN(k) || k < 0) return setError('Captura cantidad mayor que cero y costo válido.');
              setGuardando(true);
              try {
                await almacenesService.registrarStockInicial(almacenId, producto.id, { cantidad: n, costoUnitario: k });
                avisar('Stock inicial registrado.');
                cerrar();
                alGuardar();
              } catch (e) {
                setError(mensajeDe(e));
              } finally {
                setGuardando(false);
              }
            }}
          />
        </Fila>
      </View>
    </Hoja>
  );
}
