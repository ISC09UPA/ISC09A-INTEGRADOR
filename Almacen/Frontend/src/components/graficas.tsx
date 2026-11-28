import { View } from 'react-native';

import type { Punto } from '@/models/punto.model';
import { Fila, Tarjeta, Texto, Vacio } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { useColores } from '@/hooks/use-colores';
import { Formato } from '@/lib/formato';

/** Barras horizontales: legibles en pantalla angosta y sin librerías de gráficas. */
export function Barras({ titulo, puntos: todos, formato = Formato.dinero }: { titulo: string; puntos: Punto[]; formato?: (v: number) => string }) {
  const c = useColores();
  // Los periodos sin movimiento solo alargan la lista en el teléfono.
  const puntos = todos.filter((p) => p.valor !== 0);
  const maximo = Math.max(...puntos.map((p) => Math.abs(p.valor)), 0);
  return (
    <Tarjeta style={{ gap: Spacing.two, marginBottom: Spacing.three }}>
      <Texto tipo="fuerte">{titulo}</Texto>
      {puntos.length === 0 ? <Vacio texto="Sin datos en el periodo." /> : null}
      {puntos.map((p, i) => (
        <View key={`${p.etiqueta}-${i}`} style={{ gap: 2 }}>
          <Fila style={{ justifyContent: 'space-between' }}>
            <Texto tipo="pequeno" style={{ flex: 1 }} numberOfLines={1}>
              {p.etiqueta}
            </Texto>
            <Texto tipo="pequeno" color={c.text}>
              {formato(p.valor)}
            </Texto>
          </Fila>
          <View style={{ height: 8, borderRadius: 4, backgroundColor: c.line, overflow: 'hidden' }}>
            <View style={{ height: 8, width: `${maximo ? (Math.abs(p.valor) / maximo) * 100 : 0}%`, backgroundColor: c.primary, borderRadius: 4 }} />
          </View>
        </View>
      ))}
    </Tarjeta>
  );
}

/** Compras vs. ventas por periodo (barras dobles). */
export function BarrasDobles({ titulo, periodos, a, b, nombreA, nombreB }: { titulo: string; periodos: string[]; a: number[]; b: number[]; nombreA: string; nombreB: string }) {
  const c = useColores();
  const maximo = Math.max(...a, ...b, 0);
  const barra = (v: number, color: string) => (
    <View style={{ height: 8, borderRadius: 4, backgroundColor: c.line, overflow: 'hidden' }}>
      <View style={{ height: 8, width: `${maximo ? (v / maximo) * 100 : 0}%`, backgroundColor: color, borderRadius: 4 }} />
    </View>
  );
  return (
    <Tarjeta style={{ gap: Spacing.two, marginBottom: Spacing.three }}>
      <Texto tipo="fuerte">{titulo}</Texto>
      <Fila gap={Spacing.three}>
        <Fila gap={6}>
          <View style={{ width: 10, height: 10, borderRadius: 5, backgroundColor: c.success }} />
          <Texto tipo="pequeno">{nombreA}</Texto>
        </Fila>
        <Fila gap={6}>
          <View style={{ width: 10, height: 10, borderRadius: 5, backgroundColor: c.error }} />
          <Texto tipo="pequeno">{nombreB}</Texto>
        </Fila>
      </Fila>
      {periodos.length === 0 ? <Vacio texto="Sin datos en el periodo." /> : null}
      {periodos.map((p, i) => (
        <View key={p} style={{ gap: 3 }}>
          <Fila style={{ justifyContent: 'space-between' }}>
            <Texto tipo="pequeno">{p}</Texto>
            <Texto tipo="pequeno">
              {Formato.dinero(a[i] ?? 0)} / {Formato.dinero(b[i] ?? 0)}
            </Texto>
          </Fila>
          {barra(a[i] ?? 0, c.success)}
          {barra(b[i] ?? 0, c.error)}
        </View>
      ))}
    </Tarjeta>
  );
}
