import { StyleSheet, View } from 'react-native';
import { useRouter } from 'expo-router';
import HeroCard from '@/components/HeroCard';
import StatCard from '@/components/StatCard';
import ListaLockers from '@/components/ListaLockers';
import Seccion from '@/components/ui/Seccion';
import { colors } from '@/constants/tokens';
import type { DatosLockers } from '@/hooks/useLockers';

export default function AdminHome({ lockers, cargando, error }: DatosLockers) {
  const router = useRouter();
  const disponibles = lockers.filter((l) => l.estado === 'Disponible').length;
  const ocupados = lockers.filter((l) => l.estado === 'Ocupado').length;

  return (
    <View style={{ gap: 16 }}>
      <HeroCard
        chip="Tiempo real"
        titulo={'Tus lockers,\nbajo control.'}
        texto={`${lockers.length} lockers registrados`}
        boton="Ver todos los lockers"
        icono="arrow-right"
        onPress={() => router.push('/lockers')}
      />

      <View style={s.fila}>
        <StatCard valor={disponibles} etiqueta="Disponibles" color={colors.disponible} />
        <StatCard valor={ocupados} etiqueta="Ocupados" color={colors.ocupado} />
        <StatCard valor={0} etiqueta="Alertas" color={colors.alerta} />
      </View>

      <Seccion titulo="Estado de lockers">
        <ListaLockers lockers={lockers} cargando={cargando} error={error} />
      </Seccion>
    </View>
  );
}

const s = StyleSheet.create({ fila: { flexDirection: 'row', gap: 10 } });
