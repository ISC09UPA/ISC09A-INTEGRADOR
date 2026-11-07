import { MaterialIcons } from '@expo/vector-icons';
import type { ComponentProps, ReactNode } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { colores } from '@/theme/tokens';

interface Props {
  icono: ComponentProps<typeof MaterialIcons>['name'];
  colorIcono: string;
  fondoIcono: string;
  etiqueta: string;
  valor: string;
  /** Texto a la derecha del valor (p. ej. "Óptimo (45-65%)"). */
  detalle?: string;
  /** Fila de control debajo (FilaAccion). */
  children?: ReactNode;
}

/** Tarjeta de telemetría: ícono + etiqueta + valor grande + fila de actuador. */
export function TarjetaMetrica({ icono, colorIcono, fondoIcono, etiqueta, valor, detalle, children }: Props) {
  return (
    <View style={s.tarjeta}>
      <View style={s.encabezado}>
        <View style={s.fila}>
          <View style={[s.icono, { backgroundColor: fondoIcono }]}>
            <MaterialIcons name={icono} size={22} color={colorIcono} />
          </View>
          <View>
            <Text style={s.sub}>{etiqueta}</Text>
            <Text style={s.valor}>{valor}</Text>
          </View>
        </View>
        {detalle && <Text style={s.detalle}>{detalle}</Text>}
      </View>
      {children}
    </View>
  );
}

const s = StyleSheet.create({
  tarjeta: { backgroundColor: colores.surfaceContainerLowest, borderRadius: 16, padding: 16, gap: 8 },
  encabezado: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  fila: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  icono: { width: 40, height: 40, borderRadius: 12, alignItems: 'center', justifyContent: 'center' },
  sub: { fontSize: 12, color: colores.onSurfaceVariant },
  valor: { fontSize: 22, fontWeight: '700', color: colores.onSurface },
  detalle: { fontSize: 12, fontWeight: '600', color: colores.primary },
});
