import { ActivityIndicator, StyleSheet, Text, View } from 'react-native';
import LockerItem from './LockerItem';
import { colors, font } from '@/constants/tokens';
import type { DatosLockers } from '@/hooks/useLockers';

export default function ListaLockers({ lockers, cargando, error }: DatosLockers) {
  if (cargando) return <ActivityIndicator color={colors.verdeOscuro} style={{ marginVertical: 24 }} />;
  if (error) return <Text style={s.error}>{error}</Text>;
  if (lockers.length === 0) return <Text style={s.vacio}>No hay lockers registrados.</Text>;

  return (
    <View style={{ gap: 10 }}>
      {lockers.map((l) => (
        <LockerItem key={l.id} locker={l} />
      ))}
    </View>
  );
}

const s = StyleSheet.create({
  error: { fontFamily: font.regular, color: colors.alerta, textAlign: 'center', marginVertical: 16 },
  vacio: { fontFamily: font.regular, color: colors.textoSuave, textAlign: 'center', marginVertical: 16 },
});
