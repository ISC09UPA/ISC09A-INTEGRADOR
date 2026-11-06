import { StyleSheet, Text, View } from 'react-native';
import Pantalla from '@/components/ui/Pantalla';
import Boton from '@/components/ui/Boton';
import { useAuth } from '@/context/AuthContext';
import { colors, font, radius } from '@/constants/tokens';

export default function Perfil() {
  const { usuario, cerrarSesion } = useAuth();
  if (!usuario) return null;

  return (
    <Pantalla>
      <Text style={s.titulo}>Perfil</Text>

      <View style={s.card}>
        <Fila etiqueta="Nombre" valor={usuario.nombre} />
        <Fila etiqueta="Correo" valor={usuario.email} />
        <Fila etiqueta="Rol" valor={usuario.rol} />
        <Fila etiqueta="Rostro registrado" valor={usuario.tieneRostro ? 'Sí' : 'No'} />
      </View>

      <Boton titulo="Cerrar sesión" variante="borde" icono="log-out" onPress={cerrarSesion} />
    </Pantalla>
  );
}

function Fila({ etiqueta, valor }: { etiqueta: string; valor: string }) {
  return (
    <View style={s.fila}>
      <Text style={s.etiqueta}>{etiqueta}</Text>
      <Text style={s.valor}>{valor}</Text>
    </View>
  );
}

const s = StyleSheet.create({
  titulo: { fontFamily: font.bold, fontSize: 28, color: colors.texto },
  card: {
    backgroundColor: colors.superficie, borderWidth: 1, borderColor: colors.borde,
    borderRadius: radius.md, padding: 16, gap: 14,
  },
  fila: { flexDirection: 'row', justifyContent: 'space-between', gap: 12 },
  etiqueta: { fontFamily: font.regular, color: colors.textoSuave },
  valor: { fontFamily: font.medium, color: colors.texto, flexShrink: 1, textAlign: 'right' },
});
