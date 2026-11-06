import { StyleSheet, Text, View } from 'react-native';
import { Feather } from '@expo/vector-icons';
import { colors, estadoColor, font, radius } from '@/constants/tokens';
import type { Locker } from '@/types';

export default function LockerItem({ locker }: { locker: Locker }) {
  const libre = locker.estado === 'Disponible';

  return (
    <View style={s.card}>
      <View style={[s.icono, { backgroundColor: libre ? colors.verdeSuave : colors.gris }]}>
        <Feather name="archive" size={20} color={libre ? colors.verdeOscuro : colors.textoSuave} />
      </View>
      <View style={{ flex: 1 }}>
        <Text style={s.titulo}>Locker {locker.codigo}</Text>
        <Text style={[s.estado, libre && { color: colors.disponible }]}>{locker.estado}</Text>
      </View>
      <View style={[s.punto, { backgroundColor: estadoColor[locker.estado] }]} />
    </View>
  );
}

const s = StyleSheet.create({
  card: {
    flexDirection: 'row', alignItems: 'center', gap: 14,
    backgroundColor: colors.superficie, borderWidth: 1, borderColor: colors.borde,
    borderRadius: radius.md, padding: 14,
  },
  icono: { width: 44, height: 44, borderRadius: radius.sm, alignItems: 'center', justifyContent: 'center' },
  titulo: { fontFamily: font.bold, fontSize: 15, color: colors.texto },
  estado: { fontFamily: font.regular, fontSize: 13, color: colors.textoSuave },
  punto: { width: 10, height: 10, borderRadius: 5 },
});
