import { MaterialIcons } from '@expo/vector-icons';
import { StyleSheet, Text, View } from 'react-native';
import { colores } from '@/theme/tokens';

/** Diagnóstico IA de la planta seleccionada. */
export function DiagnosticoCard({ vigorPct, descripcion }: { vigorPct: number; descripcion: string }) {
  return (
    <View style={s.tarjeta}>
      <View style={s.icono}>
        <MaterialIcons name="psychology" size={22} color={colores.onPrimaryContainer} />
      </View>
      <View style={{ flex: 1 }}>
        <View style={s.fila}>
          <Text style={s.titulo}>Diagnóstico IA</Text>
          <View style={s.badge}>
            <Text style={s.badgeTexto}>{vigorPct}% Vigor</Text>
          </View>
        </View>
        <Text style={s.sub} numberOfLines={2}>
          {descripcion}
        </Text>
      </View>
    </View>
  );
}

const s = StyleSheet.create({
  tarjeta: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
    borderRadius: 16,
    padding: 16,
    backgroundColor: colores.surfaceContainerLowest,
  },
  icono: {
    width: 40,
    height: 40,
    borderRadius: 12,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: colores.primaryContainer,
  },
  fila: { flexDirection: 'row', alignItems: 'center' },
  titulo: { fontSize: 16, fontWeight: '600', color: colores.onSurface },
  badge: { borderRadius: 999, paddingHorizontal: 8, paddingVertical: 2, marginLeft: 6, backgroundColor: colores.primaryContainer },
  badgeTexto: { fontSize: 11, fontWeight: '600', color: colores.onPrimaryContainer },
  sub: { fontSize: 12, color: colores.onSurfaceVariant },
});
