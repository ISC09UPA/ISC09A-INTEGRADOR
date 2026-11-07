import { MaterialIcons } from '@expo/vector-icons';
import { StyleSheet, Text, View } from 'react-native';
import { colores } from '@/theme/tokens';

/** Banner superior: motor de reglas en modo autónomo y cuántas están activas. */
export function BannerMotor({ activas, total }: { activas: number; total: number }) {
  return (
    <View style={s.banner}>
      <View style={s.icono}>
        <MaterialIcons name="psychology" size={22} color={colores.onPrimary} />
      </View>
      <View>
        <Text style={s.titulo}>Motor de Reglas</Text>
        <Text style={s.sub}>
          Modo Autónomo · {activas}/{total} activas
        </Text>
      </View>
    </View>
  );
}

const s = StyleSheet.create({
  banner: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    borderRadius: 16,
    padding: 12,
    backgroundColor: colores.primary,
  },
  icono: {
    width: 40,
    height: 40,
    borderRadius: 12,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: 'rgba(255,255,255,0.2)',
  },
  titulo: { fontSize: 16, fontWeight: '600', color: colores.onPrimary },
  sub: { fontSize: 12, color: colores.onPrimary, opacity: 0.9 },
});
