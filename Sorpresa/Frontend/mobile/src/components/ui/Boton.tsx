import { ActivityIndicator, Pressable, StyleSheet, Text } from 'react-native';
import { Feather } from '@expo/vector-icons';
import { colors, font, radius } from '@/constants/tokens';

interface Props {
  titulo: string;
  onPress?: () => void;
  variante?: 'oscuro' | 'claro' | 'borde';
  icono?: keyof typeof Feather.glyphMap;
  cargando?: boolean;
  disabled?: boolean;
}

const fondo = { oscuro: colors.verdeOscuro, claro: colors.verdeClaro, borde: 'transparent' };
const texto = { oscuro: '#FFFFFF', claro: colors.verdeOscuro, borde: colors.texto };

export default function Boton({ titulo, onPress, variante = 'oscuro', icono, cargando, disabled }: Props) {
  return (
    <Pressable
      onPress={onPress}
      disabled={disabled || cargando}
      style={({ pressed }) => [
        s.base,
        { backgroundColor: fondo[variante], opacity: pressed || disabled ? 0.8 : 1 },
        variante === 'borde' && s.borde,
      ]}
    >
      {cargando ? (
        <ActivityIndicator color={texto[variante]} />
      ) : (
        <>
          {icono && <Feather name={icono} size={18} color={texto[variante]} />}
          <Text style={[s.txt, { color: texto[variante] }]}>{titulo}</Text>
        </>
      )}
    </Pressable>
  );
}

const s = StyleSheet.create({
  base: {
    flexDirection: 'row', alignItems: 'center', justifyContent: 'center',
    gap: 8, paddingVertical: 16, borderRadius: radius.lg,
  },
  borde: { borderWidth: 1, borderColor: colors.borde },
  txt: { fontFamily: font.medium, fontSize: 16 },
});
