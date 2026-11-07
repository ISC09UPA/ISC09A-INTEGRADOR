import { colores } from '@/theme/tokens';
import { StyleSheet, Text, View } from 'react-native';

export default function Camara() {
  return (
    <View style={styles.contenedor}>
      <Text style={styles.titulo}>Cámara</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  contenedor: { flex: 1, alignItems: 'center', justifyContent: 'center' },
  titulo: { fontSize: 20, fontWeight: '600', color: colores.onSurface },
});
