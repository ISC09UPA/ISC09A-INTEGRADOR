import { MaterialIcons } from '@expo/vector-icons';
import { StyleSheet, Text, View } from 'react-native';
import { colores } from '@/theme/tokens';

/** Logo + título de la app y estado de la RPi. */
export function HeaderInicio({ online = true }: { online?: boolean }) {
  return (
    <View style={s.header}>
      <View style={s.fila}>
        <View style={s.icono}>
          <MaterialIcons name="eco" size={22} color={colores.primary} />
        </View>
        <View>
          <Text style={s.titulo}>RiegoSmart IA</Text>
          <Text style={s.sub}>Inicio</Text>
        </View>
      </View>
      <View style={[s.pill, { backgroundColor: online ? colores.primaryContainer : colores.errorContainer }]}>
        <View style={[s.punto, { backgroundColor: online ? colores.primary : colores.error }]} />
        <Text style={[s.pillTexto, { color: online ? colores.onPrimaryContainer : colores.onErrorContainer }]}>
          {online ? 'RPi Online' : 'RPi Offline'}
        </Text>
      </View>
    </View>
  );
}

const s = StyleSheet.create({
  header: {
    height: 64,
    marginTop: 40,
    paddingHorizontal: 16,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  fila: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  icono: {
    width: 40,
    height: 40,
    borderRadius: 12,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: colores.primaryContainer,
  },
  titulo: { fontSize: 16, fontWeight: '600', color: colores.onSurface },
  sub: { fontSize: 12, color: colores.onSurfaceVariant },
  pill: { flexDirection: 'row', alignItems: 'center', gap: 6, borderRadius: 999, paddingHorizontal: 10, paddingVertical: 4 },
  punto: { width: 8, height: 8, borderRadius: 4 },
  pillTexto: { fontSize: 11, fontWeight: '500' },
});
