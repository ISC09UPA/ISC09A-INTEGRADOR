import { Pressable, StyleSheet, Text, View } from 'react-native';
import { Feather } from '@expo/vector-icons';
import { useRouter } from 'expo-router';
import { useAuth } from '@/context/AuthContext';
import { colors, font } from '@/constants/tokens';

export default function Encabezado({ etiqueta }: { etiqueta: string }) {
  const { usuario } = useAuth();
  const router = useRouter();
  const nombre = usuario?.nombre.split(' ')[0] ?? '';

  return (
    <View style={s.fila}>
      <View>
        <Text style={s.etiqueta}>{etiqueta}</Text>
        <Text style={s.saludo}>Hola, {nombre}</Text>
      </View>
      <Pressable style={s.avatar} onPress={() => router.push('/perfil')}>
        <Feather name="user" size={20} color={colors.verdeOscuro} />
      </Pressable>
    </View>
  );
}

const s = StyleSheet.create({
  fila: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  etiqueta: { fontFamily: font.medium, fontSize: 11, letterSpacing: 1.5, color: colors.textoSuave },
  saludo: { fontFamily: font.bold, fontSize: 26, color: colors.texto },
  avatar: {
    width: 44, height: 44, borderRadius: 22,
    backgroundColor: colors.verdeSuave, alignItems: 'center', justifyContent: 'center',
  },
});
