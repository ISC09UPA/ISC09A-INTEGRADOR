import { Alert, StyleSheet, View } from 'react-native';
import { useRouter } from 'expo-router';
import HeroCard from '@/components/HeroCard';
import QuickCard from '@/components/QuickCard';
import ListaLockers from '@/components/ListaLockers';
import Seccion from '@/components/ui/Seccion';
import type { DatosLockers } from '@/hooks/useLockers';

export default function UserHome({ lockers, cargando, error }: DatosLockers) {
  const router = useRouter();

  return (
    <View style={{ gap: 16 }}>
      <HeroCard
        chip="Sin renta activa"
        titulo={'Guarda lo\nimportante.'}
        texto="Renta un locker seguro en menos de un minuto."
        boton="Rentar un locker"
        onPress={() => router.push('/lockers')}
      />

      <View style={s.fila}>
        <QuickCard
          icono="clock"
          titulo="Extender renta"
          subtitulo="Agrega más tiempo"
          onPress={() => Alert.alert('Próximamente', 'Se activará con el módulo de rentas.')}
        />
        <QuickCard
          icono="bell"
          titulo="Todo en orden"
          subtitulo="Sin alertas nuevas"
          onPress={() => router.push('/actividad')}
        />
      </View>

      <Seccion titulo="Lockers cercanos">
        <ListaLockers lockers={lockers.slice(0, 3)} cargando={cargando} error={error} />
      </Seccion>
    </View>
  );
}

const s = StyleSheet.create({ fila: { flexDirection: 'row', gap: 12 } });
