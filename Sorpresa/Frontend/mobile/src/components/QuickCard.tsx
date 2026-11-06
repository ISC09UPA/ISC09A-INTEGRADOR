import { Pressable, StyleSheet, Text } from 'react-native';
import { Feather } from '@expo/vector-icons';
import { colors, font, radius } from '@/constants/tokens';

interface Props {
  icono: keyof typeof Feather.glyphMap;
  titulo: string;
  subtitulo: string;
  onPress: () => void;
}

export default function QuickCard({ icono, titulo, subtitulo, onPress }: Props) {
  return (
    <Pressable style={s.card} onPress={onPress}>
      <Feather name={icono} size={20} color={colors.verdeOscuro} />
      <Text style={s.titulo}>{titulo}</Text>
      <Text style={s.sub}>{subtitulo}</Text>
    </Pressable>
  );
}

const s = StyleSheet.create({
  card: {
    flex: 1, backgroundColor: colors.superficie, borderWidth: 1, borderColor: colors.borde,
    borderRadius: radius.md, padding: 16, gap: 4,
  },
  titulo: { fontFamily: font.bold, fontSize: 15, color: colors.texto, marginTop: 14 },
  sub: { fontFamily: font.regular, fontSize: 13, color: colors.textoSuave },
});
