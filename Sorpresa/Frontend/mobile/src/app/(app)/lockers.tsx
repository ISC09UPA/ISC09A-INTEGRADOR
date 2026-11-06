import { StyleSheet, Text } from 'react-native';
import Pantalla from '@/components/ui/Pantalla';
import ListaLockers from '@/components/ListaLockers';
import { useModo } from '@/context/ModoContext';
import { useLockers } from '@/hooks/useLockers';
import { colors, font } from '@/constants/tokens';

export default function Lockers() {
  const { modo } = useModo();
  const datos = useLockers();

  return (
    <Pantalla onRefrescar={datos.recargar}>
      <Text style={s.titulo}>Lockers</Text>
      <Text style={s.sub}>
        {modo === 'admin' ? 'Estado de todos los lockers' : 'Elige un locker disponible para rentar'}
      </Text>
      <ListaLockers {...datos} />
    </Pantalla>
  );
}

const s = StyleSheet.create({
  titulo: { fontFamily: font.bold, fontSize: 28, color: colors.texto },
  sub: { fontFamily: font.regular, fontSize: 14, color: colors.textoSuave, marginTop: -8 },
});
