import { StyleSheet, Text, View } from 'react-native';
import { Feather } from '@expo/vector-icons';
import { colors, text } from '@/constants/casa-theme';

/** Encabezado pequeño "CASA DOMÓTICA" con el ícono de la casa. */
export function BrandLabel() {
  return (
    <View style={styles.row}>
      <Feather name="home" size={16} color={colors.teal} />
      <Text style={styles.label}>Casa domótica</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  label: { ...text('semibold', 13, 'teal'), letterSpacing: 1.2, textTransform: 'uppercase' },
});
