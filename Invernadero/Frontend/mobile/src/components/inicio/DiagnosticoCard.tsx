
import { MaterialIcons } from '@expo/vector-icons';
import { StyleSheet, Text, View } from 'react-native';
import { useColorScheme } from 'nativewind';
import { colores } from '@/theme/tokens';

/** Diagnóstico IA de la planta seleccionada. */
export function DiagnosticoCard({
  vigorPct,
  descripcion,
}: {
  vigorPct: number;
  descripcion: string;
}) {
  const { colorScheme } = useColorScheme();
  const tema = colorScheme === 'dark' ? colores.oscuro : colores.claro;

  return (
    <View style={[s.tarjeta, { backgroundColor: tema.surfaceContainerLowest }]}>
      <View style={[s.icono, { backgroundColor: tema.primaryContainer }]}>
        <MaterialIcons
          name="psychology"
          size={22}
          color={tema.onPrimaryContainer}
        />
      </View>

      <View style={{ flex: 1 }}>
        <View style={s.fila}>
          <Text style={[s.titulo, { color: tema.onSurface }]}>
            Diagnóstico IA
          </Text>

          <View style={[s.badge, { backgroundColor: tema.primaryContainer }]}>
            <Text style={[s.badgeTexto, { color: tema.onPrimaryContainer }]}>
              {vigorPct}% Vigor
            </Text>
          </View>
        </View>

        <Text
          style={[s.sub, { color: tema.onSurfaceVariant }]}
          numberOfLines={2}
        >
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
  },
  icono: {
    width: 40,
    height: 40,
    borderRadius: 12,
    alignItems: 'center',
    justifyContent: 'center',
  },
  fila: { flexDirection: 'row', alignItems: 'center' },
  titulo: { fontSize: 16, fontWeight: '600' },
  badge: {
    borderRadius: 999,
    paddingHorizontal: 8,
    paddingVertical: 2,
    marginLeft: 6,
  },
  badgeTexto: { fontSize: 11, fontWeight: '600' },
  sub: { fontSize: 12 },
});
