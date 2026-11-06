import { StyleSheet, Text, View } from 'react-native';
import { colors, font, radius } from '@/constants/tokens';

export default function StatCard({ valor, etiqueta, color }: { valor: number; etiqueta: string; color: string }) {
  return (
    <View style={s.card}>
      <View style={[s.punto, { backgroundColor: color }]} />
      <Text style={s.valor}>{valor}</Text>
      <Text style={s.etiqueta}>{etiqueta}</Text>
    </View>
  );
}

const s = StyleSheet.create({
  card: {
    flex: 1, backgroundColor: colors.superficie, borderWidth: 1, borderColor: colors.borde,
    borderRadius: radius.md, padding: 14, gap: 2,
  },
  punto: { width: 8, height: 8, borderRadius: 4, marginBottom: 8 },
  valor: { fontFamily: font.bold, fontSize: 26, color: colors.texto },
  etiqueta: { fontFamily: font.regular, fontSize: 12, color: colors.textoSuave },
});
