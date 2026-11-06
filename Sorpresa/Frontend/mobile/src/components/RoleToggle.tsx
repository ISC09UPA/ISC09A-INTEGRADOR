import { Pressable, StyleSheet, Text, View } from 'react-native';
import { useModo, type Modo } from '@/context/ModoContext';
import { colors, font, radius } from '@/constants/tokens';

const opciones: { valor: Modo; texto: string }[] = [
  { valor: 'usuario', texto: 'Usuario' },
  { valor: 'admin', texto: 'Administrador' },
];

export default function RoleToggle() {
  const { modo, setModo } = useModo();

  return (
    <View style={s.contenedor}>
      {opciones.map((o) => {
        const activo = modo === o.valor;
        return (
          <Pressable key={o.valor} style={[s.opcion, activo && s.activa]} onPress={() => setModo(o.valor)}>
            <Text style={[s.txt, activo && s.txtActivo]}>{o.texto}</Text>
          </Pressable>
        );
      })}
    </View>
  );
}

const s = StyleSheet.create({
  contenedor: { flexDirection: 'row', backgroundColor: colors.gris, borderRadius: radius.lg, padding: 4 },
  opcion: { flex: 1, paddingVertical: 12, borderRadius: radius.md, alignItems: 'center' },
  activa: {
    backgroundColor: colors.superficie,
    shadowColor: '#000', shadowOpacity: 0.06, shadowRadius: 4, shadowOffset: { width: 0, height: 1 }, elevation: 1,
  },
  txt: { fontFamily: font.medium, fontSize: 15, color: colors.textoSuave },
  txtActivo: { color: colors.texto },
});
