import { StyleSheet, Text, View } from 'react-native';
import { Feather } from '@expo/vector-icons';
import Boton from '@/components/ui/Boton';
import { colors, font, radius } from '@/constants/tokens';

interface Props {
  chip: string;
  titulo: string;
  texto: string;
  boton: string;
  icono?: keyof typeof Feather.glyphMap;
  onPress: () => void;
}

export default function HeroCard({ chip, titulo, texto, boton, icono = 'plus', onPress }: Props) {
  return (
    <View style={s.card}>
      <View style={s.fila}>
        <View style={s.chip}>
          <Text style={s.chipTxt}>{chip}</Text>
        </View>
        <Feather name="shield" size={20} color={colors.verdeClaro} />
      </View>
      <Text style={s.titulo}>{titulo}</Text>
      <Text style={s.texto}>{texto}</Text>
      <Boton titulo={boton} icono={icono} variante="claro" onPress={onPress} />
    </View>
  );
}

const s = StyleSheet.create({
  card: { backgroundColor: colors.verdeOscuro, borderRadius: radius.lg, padding: 22, gap: 14 },
  fila: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  chip: { backgroundColor: 'rgba(255,255,255,0.14)', paddingHorizontal: 12, paddingVertical: 5, borderRadius: radius.pill },
  chipTxt: { color: '#E8F5EA', fontFamily: font.medium, fontSize: 12 },
  titulo: { color: '#FFFFFF', fontFamily: font.bold, fontSize: 34, lineHeight: 38, marginTop: 6 },
  texto: { color: colors.verdeClaro, fontFamily: font.regular, fontSize: 15, marginBottom: 6 },
});
