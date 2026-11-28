import { useEffect, useState } from 'react';
import { Pressable, View } from 'react-native';

import type { ConsultaStockIot } from '@/models/consulta-stock-iot.model';
import type { SemaforoProducto } from '@/models/semaforo-stock.model';
import { stockService } from '@/services';
import { ListaPantalla } from '@/components/lista';
import { Busqueda, Chip, Fila, Tarjeta, Texto } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { useColores } from '@/hooks/use-colores';
import { usePaginado } from '@/hooks/use-datos';
import { Formato } from '@/lib/formato';
import type { NivelStock } from '@/types/enums';

const NIVELES: NivelStock[] = ['Rojo', 'Amarillo', 'Verde'];
const colorChip = (n: NivelStock) => (n === 'Verde' ? 'success' : n === 'Amarillo' ? 'warning' : 'error');

/**
 * Comprobar stock (ComprobarStock.razor): semáforo por producto con la existencia de todos los almacenes.
 * Rojo sin existencia, amarillo en o por debajo del mínimo, verde arriba. El lector del embebido usa los mismos colores en sus LEDs.
 */
export default function ComprobarStock() {
  const c = useColores();
  const [vista, setVista] = useState<'productos' | 'lector'>('productos');
  const [busqueda, setBusqueda] = useState('');
  const [nivel, setNivel] = useState<NivelStock | undefined>();
  const [conteo, setConteo] = useState({ Verde: 0, Amarillo: 0, Rojo: 0 });

  const productos = usePaginado<SemaforoProducto>(
    async (pagina) => {
      const r = await stockService.semaforo({ busqueda, nivel, pagina });
      setConteo({ Verde: r.verdes, Amarillo: r.amarillos, Rojo: r.rojos });
      return r.productos;
    },
    [busqueda, nivel],
  );
  const consultas = usePaginado<ConsultaStockIot>((pagina) => stockService.consultasLector(pagina), []);
  const { recargar: recargarConsultas } = consultas;
  const { recargar: recargarProductos } = productos;
  const primeraConsulta = consultas.elementos[0]?.id;

  // Con la pestaña del lector abierta, cada 3 s revisa si llegó una consulta nueva y solo entonces recarga.
  useEffect(() => {
    if (vista !== 'lector') return;
    let ocupado = false;
    const reloj = setInterval(async () => {
      if (ocupado) return;
      ocupado = true;
      try {
        const ultima = (await stockService.consultasLector(1, 1)).elementos[0]?.id;
        if (ultima && ultima !== primeraConsulta) {
          recargarConsultas();
          recargarProductos();
        }
      } catch {
        // Falla de red pasajera: se reintenta en el siguiente ciclo.
      } finally {
        ocupado = false;
      }
    }, 3000);
    return () => clearInterval(reloj);
  }, [vista, primeraConsulta, recargarConsultas, recargarProductos]);

  const tono = (n: NivelStock) => (n === 'Verde' ? c.success : n === 'Amarillo' ? c.warning : c.error);
  const punto = (n: NivelStock, tamano = 14) => <View style={{ width: tamano, height: tamano, borderRadius: tamano / 2, backgroundColor: tono(n) }} />;

  const pestanas = (
    <Fila gap={6}>
      {(['productos', 'lector'] as const).map((v) => (
        <Pressable
          key={v}
          accessibilityRole="tab"
          accessibilityState={{ selected: vista === v }}
          onPress={() => setVista(v)}
          style={{ flex: 1, borderWidth: 1, borderColor: vista === v ? c.primary : c.line, backgroundColor: vista === v ? c.primary + '1A' : c.surface, borderRadius: 8, paddingVertical: 8, alignItems: 'center' }}>
          <Texto tipo={vista === v ? 'fuerte' : 'cuerpo'} color={vista === v ? c.primary : undefined}>
            {v === 'productos' ? 'Productos' : 'Consultas del lector'}
          </Texto>
        </Pressable>
      ))}
    </Fila>
  );

  if (vista === 'lector')
    return (
      <ListaPantalla
        {...consultas}
        alRecargar={consultas.recargar}
        alLlegarAlFinal={consultas.cargarMas}
        clave={(x) => x.id}
        vacio="Todavía no se ha comprobado stock desde el lector."
        encabezado={
          <View style={{ gap: Spacing.two }}>
            {pestanas}
            <Texto tipo="secundario">Piezas que pasaron por el lector mientras nadie tenía el modo inteligente de venta. Se actualiza sola.</Texto>
          </View>
        }
        fila={(x) => (
          <Tarjeta style={{ gap: 4 }}>
            <Fila style={{ justifyContent: 'space-between' }}>
              <Texto tipo="fuerte" style={{ flex: 1 }} numberOfLines={1}>
                {x.producto ?? `Familia ${x.familia}`}
              </Texto>
              {x.nivel ? <Chip texto={x.nivel} color={colorChip(x.nivel)} /> : null}
            </Fila>
            <Texto tipo="secundario">
              {x.motivo}
              {x.existencia !== undefined ? ` · existencia ${Formato.cantidad(x.existencia)}` : ''}
            </Texto>
            <Texto tipo="pequeno">
              {Formato.fechaHora(x.fecha)} · pieza {x.uid}
            </Texto>
          </Tarjeta>
        )}
      />
    );

  return (
    <ListaPantalla
      {...productos}
      alRecargar={productos.recargar}
      alLlegarAlFinal={productos.cargarMas}
      clave={(p) => p.productoId}
      vacio="No hay productos que coincidan."
      encabezado={
        <View style={{ gap: Spacing.two }}>
          {pestanas}
          <Texto tipo="secundario">Existencia de todos los almacenes. Toca un color para ver solo esos productos.</Texto>
          <Fila gap={6}>
            {NIVELES.map((n) => (
              <Pressable
                key={n}
                accessibilityRole="button"
                accessibilityLabel={`${n}: ${conteo[n]} productos`}
                onPress={() => setNivel((actual) => (actual === n ? undefined : n))}
                style={{ flex: 1, flexDirection: 'row', alignItems: 'center', gap: 8, borderWidth: nivel === n ? 2 : 1, borderColor: nivel === n ? tono(n) : c.line, backgroundColor: c.surface, borderRadius: 8, padding: Spacing.two }}>
                {punto(n)}
                <View>
                  <Texto tipo="pequeno">{n}</Texto>
                  <Texto tipo="subtitulo">{conteo[n]}</Texto>
                </View>
              </Pressable>
            ))}
          </Fila>
          <Busqueda valor={busqueda} alCambiar={setBusqueda} placeholder="Producto o familia NFC" />
        </View>
      }
      fila={(p) => (
        <Tarjeta style={{ gap: 6 }}>
          <Fila style={{ justifyContent: 'space-between' }}>
            <Fila gap={8} style={{ flex: 1 }}>
              {punto(p.nivel)}
              <Texto tipo="fuerte" style={{ flex: 1 }} numberOfLines={1}>
                {p.referencia} <Texto tipo="secundario">{p.descripcion}</Texto>
              </Texto>
            </Fila>
            <Chip texto={p.nivel} color={colorChip(p.nivel)} />
          </Fila>
          <Fila style={{ justifyContent: 'space-between', flexWrap: 'wrap' }}>
            <Texto tipo="subtitulo" color={tono(p.nivel)}>
              {Formato.cantidad(p.existencia)} {p.unidad}
            </Texto>
            <Texto tipo="secundario">Mínimo {p.minimo > 0 ? Formato.cantidad(p.minimo) : '—'}</Texto>
          </Fila>
          {p.almacenes.length ? (
            p.almacenes.map((a) => (
              <Fila key={a.almacenId} gap={6}>
                {punto(a.nivel, 8)}
                <Texto tipo="pequeno">
                  {a.almacen}: {Formato.cantidad(a.cantidad)}
                  {a.minimo > 0 ? ` (mín. ${Formato.cantidad(a.minimo)})` : ''}
                </Texto>
              </Fila>
            ))
          ) : (
            <Texto tipo="pequeno">Sin existencias en ningún almacén</Texto>
          )}
          {p.familiaNfc ? <Texto tipo="pequeno">Familia NFC {p.familiaNfc}</Texto> : null}
        </Tarjeta>
      )}
    />
  );
}
