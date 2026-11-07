import { StyleSheet, Text, View } from 'react-native';
import { text } from '@/constants/casa-theme';
import { Dot } from './dot';

/** Recuadro oscuro donde irá el video de la cámara. Por ahora es solo ilustrativo. */
export function CameraPreview({ enVivo }: { enVivo: boolean }) {
  return (
    <View style={styles.preview}>
      <Text style={styles.placeholder}>Vista ilustrativa</Text>
      <View style={styles.status}>
        <Dot color={enVivo ? '#EAF6F3' : 'rgba(255, 255, 255, 0.35)'} size={12} />
        <Text style={[styles.statusText, !enVivo && styles.statusOff]}>
          {enVivo ? 'En vivo · conectado' : 'Sesión terminada'}
        </Text>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  preview: {
    aspectRatio: 360 / 275,
    borderRadius: 24,
    backgroundColor: '#1E2F3B',
    borderWidth: 1.5,
    borderColor: 'rgba(255, 255, 255, 0.6)',
    overflow: 'hidden',
  },
  placeholder: {
    ...text('regular', 13, 'white'),
    position: 'absolute',
    top: '33%',
    left: '15%',
    letterSpacing: 0.4,
    textTransform: 'uppercase',
  },
  status: { position: 'absolute', left: 20, bottom: 22, flexDirection: 'row', alignItems: 'center', gap: 10 },
  statusText: text('regular', 15, 'white'),
  statusOff: { color: 'rgba(255, 255, 255, 0.6)' },
});
