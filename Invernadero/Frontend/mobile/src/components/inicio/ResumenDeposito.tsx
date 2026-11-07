import { MaterialIcons } from '@expo/vector-icons';
import { StyleSheet, Text, View } from 'react-native';
import { colores } from '@/theme/tokens';

interface Props {
  nivelDeposito: number;
  ultimoRiegoMl: number;
}

/** Fila de dos tarjetas: nivel del depósito y último riego. */
export function ResumenDeposito({ nivelDeposito, ultimoRiegoMl }: Props) {
  return (
    <View style={s.fila}>
      <View style={s.tarjeta}>
        <MaterialIcons name="inventory-2" size={20} color={colores.onSecondaryContainer} />
        <Text style={s.sub}>Depósito Principal</Text>
        <Text style={s.valor}>{nivelDeposito}%</Text>
        <View style={s.barraFondo}>
          <View style={[s.barra, { width: `${nivelDeposito}%` }]} />
        </View>
      </View>
      <View style={s.tarjeta}>
        <MaterialIcons name="schedule" size={20} color={colores.onPrimaryContainer} />
        <Text style={s.sub}>Último Riego</Text>
        <Text style={s.valor}>{ultimoRiegoMl} mL</Text>
        <Text style={s.sub}>Hoy, 08:30 h</Text>
      </View>
    </View>
  );
}

const s = StyleSheet.create({
  fila: { flexDirection: 'row', gap: 8 },
  tarjeta: { flex: 1, backgroundColor: colores.surfaceContainerLowest, borderRadius: 16, padding: 16, gap: 8 },
  sub: { fontSize: 12, color: colores.onSurfaceVariant },
  valor: { fontSize: 20, fontWeight: '700', color: colores.onSurface },
  barraFondo: { height: 8, borderRadius: 4, overflow: 'hidden', backgroundColor: colores.surfaceContainerHighest },
  barra: { height: '100%', borderRadius: 4, backgroundColor: colores.secondary },
});
