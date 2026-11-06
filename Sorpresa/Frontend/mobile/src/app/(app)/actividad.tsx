import { StyleSheet, Text, View } from 'react-native';
import { Feather } from '@expo/vector-icons';
import Pantalla from '@/components/ui/Pantalla';
import { useModo } from '@/context/ModoContext';
import { colors, font } from '@/constants/tokens';

export default function Actividad() {
  const { modo } = useModo();

  return (
    <Pantalla>
      <Text style={s.titulo}>Actividad</Text>
      <View style={s.vacio}>
        <Feather name="bell" size={32} color={colors.textoSuave} />
        <Text style={s.txt}>
          {modo === 'admin'
            ? 'Aquí verás las alertas y eventos de todos los lockers.'
            : 'Aquí verás tus avisos y movimientos.'}
        </Text>
      </View>
    </Pantalla>
  );
}

const s = StyleSheet.create({
  titulo: { fontFamily: font.bold, fontSize: 28, color: colors.texto },
  vacio: { alignItems: 'center', gap: 12, paddingVertical: 48 },
  txt: { fontFamily: font.regular, color: colors.textoSuave, textAlign: 'center' },
});
